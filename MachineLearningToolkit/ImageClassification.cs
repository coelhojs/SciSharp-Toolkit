﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NumSharp;
using Tensorflow;
using static Tensorflow.Binding;

namespace MachineLearningToolkit
{
    public class ImageClassification
    {
        private Graph Graph;
        private string Input_name;
        private string[] Labels;
        private string ModelDir;
        private string Output_name;

        public ImageClassification(string modelDir,
                                    string graphFile = "retrained_graph.pb", string labelFile = "label_map.txt",
                                    string input_name = "Placeholder", string output_name = "final_result")
        {
            ModelDir = modelDir;
            Graph = ImportGraph(graphFile);
            Input_name = input_name;
            Labels = LoadLabels(modelDir, labelFile);
            Output_name = output_name;
        }
        private Graph ImportGraph(string graphFile)
        {
            try
            {
                var graph = new Graph();
                graph.Import(Path.Combine(ModelDir, graphFile));

                return graph;
            }
            catch (Exception)
            {
                throw new Exception("Nao foi possivel localizar o arquivo do modelo.\nInforme o path para o arquivo .pb " +
                    "com o argumento --graphFile");
            }
        }
        private string[] LoadLabels(string modelDir, string labelFile)
        {
            return File.ReadAllLines(Path.Join(modelDir, labelFile));
        }
        public List<ClassificationInference> Classify(string listPath)
        {
            List<ClassificationInference> Results = new List<ClassificationInference>();

            var list = JsonUtil<List<string>>.ReadJsonFile(listPath);

            foreach (var image in list)
            {
                NDArray imgArr = ReadTensorFromImageFile(Path.GetFullPath(image));

                using (var sess = tf.Session(Graph))
                    Results.Add(Predict(sess, imgArr, image));
            }
            return Results;
        }
        private ClassificationInference Predict(Session sess, NDArray imgArr, string image)
        {
            var input_operation = Graph.OperationByName(Input_name);
            var output_operation = Graph.OperationByName(Output_name);

            var classificationResults = new Dictionary<string, float>();

            var results = sess.run(output_operation.outputs[0], (input_operation.outputs[0], imgArr));
            results = np.squeeze(results);

            var argsort = results.argsort<float>();
            var top_k = argsort.Data<float>()
                .Skip(results.size - 5)
                .Reverse()
                .ToArray();

            foreach (float idx in top_k)
            {
                classificationResults.Add(Labels[(int)idx], results[(int)idx]);
            }

            return new ClassificationInference()
            {
                Classifications = classificationResults,
                DateTime = DateTime.Now,
                Image = image
            };
        }

        private NDArray ReadTensorFromImageFile(string file_name,
                                int input_height = 224,
                                int input_width = 224,
                                int input_mean = 117,
                                int input_std = 1)
        {
            var graph = tf.Graph().as_default();

            var file_reader = tf.read_file(file_name, "file_reader");
            var decodeJpeg = tf.image.decode_jpeg(file_reader, channels: 3, name: "DecodeJpeg");
            var cast = tf.cast(decodeJpeg, tf.float32);
            var dims_expander = tf.expand_dims(cast, 0);
            var resize = tf.constant(new int[] { input_height, input_width });
            var bilinear = tf.image.resize_bilinear(dims_expander, resize);
            var sub = tf.subtract(bilinear, new float[] { input_mean });
            var normalized = tf.divide(sub, new float[] { input_std });

            using (var sess = tf.Session(graph))
                return sess.run(normalized);
        }
    }
}