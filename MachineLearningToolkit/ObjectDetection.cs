using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using MachineLearningToolkit.Utility;
using NumSharp;
using Tensorflow;
using static Tensorflow.Binding;

namespace MachineLearningToolkit
{
    public class ObjectDetection
    {
        private const float MIN_SCORE = 0.7f;
        private Graph Graph;
        private PbtxtItems Labels;
        private string ModelDir;
        public ObjectDetection(string modelDir, string graphFile = "frozen_inference_graph.pb", string labelFile = "label_map.pbtxt")
        {
            ModelDir = modelDir;
            Graph = ImportGraph(graphFile);
            Labels = LoadLabels(modelDir, labelFile);
        }

        public List<Result> Inference(string listPath)
        {
            try
            {
                List<Result> Results = new List<Result>();

                var list = JsonUtil<List<string>>.ReadJsonFile(listPath);

                foreach (var image in list)
                {
                    NDArray imgArr = ReadTensorFromImageFile(Path.GetFullPath(image));

                    using (var sess = tf.Session(Graph))
                        Results.Add(Predict(sess, imgArr, image));
                }
                return Results;
            }
            catch (FileNotFoundException ex)
            {
                throw new Exception($"Arquivo nao localizado {ex.Message}");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private Graph ImportGraph(string graphFile)
        {
            try
            {
                var graph = new Graph().as_default();
                graph.Import(Path.Combine(ModelDir, graphFile));

                return graph;
            }
            catch (Exception)
            {
                throw new Exception("Nao foi possivel localizar o arquivo do modelo.\nInforme o path para o arquivo .pb " +
                    "com o argumento --graphFile");
            }
        }

        private Result Predict(Session sess, NDArray imgArr, string image)
        {
            var graph = tf.get_default_graph();

            Tensor tensorNum = graph.OperationByName("num_detections");
            Tensor tensorBoxes = graph.OperationByName("detection_boxes");
            Tensor tensorScores = graph.OperationByName("detection_scores");
            Tensor tensorClasses = graph.OperationByName("detection_classes");
            Tensor imgTensor = graph.OperationByName("image_tensor");
            Tensor[] outTensorArr = new Tensor[] { tensorNum, tensorBoxes, tensorScores, tensorClasses };

            var results = sess.run(outTensorArr, new FeedItem(imgTensor, imgArr));

            return ParseResults(results, image);
        }

        private NDArray ReadTensorFromImageFile(string file_name)
        {
            var graph = tf.Graph().as_default();

            var file_reader = tf.read_file(file_name, "file_reader");
            var decodeJpeg = tf.image.decode_jpeg(file_reader, channels: 3, name: "DecodeJpeg");
            var casted = tf.cast(decodeJpeg, TF_DataType.TF_UINT8);
            var dims_expander = tf.expand_dims(casted, 0);

            using (var sess = tf.Session(graph))
                return sess.run(dims_expander);
        }

        private Result ParseResults(NDArray[] resultArr, string imagePath)
        {
            var detectionsList = new List<DetectionInference>();

            // get bitmap
            Bitmap bitmap = new Bitmap(Path.GetFullPath(imagePath));

            var detectionClasses = np.squeeze(resultArr[3]).GetData<float>();
            var detectionScores = resultArr[2].AsIterator<float>();
            var detectionBoxes = resultArr[1].GetData<float>().ToArray();

            var scores = detectionScores.Where(score => score > MIN_SCORE).ToArray();
            var classes = detectionClasses.Take(scores.Length).ToArray();

            for (int i = 0; i < scores.Length; i++)
            {
                detectionsList.Add(new DetectionInference()
                {
                    BoundingBox = CreateReactangle(bitmap, detectionBoxes, i),
                    Class = Labels.items.Where(w => w.id == detectionClasses[i]).Select(s => s.display_name).FirstOrDefault(),
                    Image = imagePath,
                    Score = scores[i]
                });
            }

            return new Result()
            {
                DateTime = DateTime.Now,
                NumDetections = scores.Length,
                Results = detectionsList
            };
        }

        private PbtxtItems LoadLabels(string modelDir, string labelFile)
        {
            try
            {
                // get pbtxt items
                return PbtxtParser.ParsePbtxtFile(Path.Combine(modelDir, labelFile));
            }
            catch (Exception)
            {
                throw new Exception("Nao foi possivel carregar o arquivo de labels." +
                    "\nVerifique o formato do arquivo e " +
                    "informe o path para o arquivo .pbtxt " +
                    "com o argumento --labelFile");
            }
        }

        private Rectangle CreateReactangle(Bitmap bitmap, float[] boxes, int i)
        {
            float top = boxes[i * 4] * bitmap.Height;
            float left = boxes[i * 4 + 1] * bitmap.Width;
            float bottom = boxes[i * 4 + 2] * bitmap.Height;
            float right = boxes[i * 4 + 3] * bitmap.Width;

            Rectangle rect = new Rectangle()
            {
                X = (int)left,
                Y = (int)top,
                Width = (int)(right - left),
                Height = (int)(bottom - top)
            };

            return rect;
        }
    }
}