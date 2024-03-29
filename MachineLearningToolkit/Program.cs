﻿using MachineLearningToolkit.Utility;
using NLog;
using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Permissions;

namespace MachineLearningToolkit
{
    public class Program
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static bool drawImages = false;
        private static string graphFile = null;
        private static string labelFile = null;
        private static string listFile = "";
        private static string logPath = "";
        private static int maxDetections = 100;
        private static string modelDir = "";
        private static float minScore = 0.7f;
        private static string outputDir = "";
        internal static Process Process;
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("Informe a função que você deseja utilizar" +
                        "'ImageClassification', 'ObjectDetection'");
                }

                for (int i = 0; i < args.Length; i++)
                {
                    string value = args[i];

                    switch (value)
                    {
                        case "--drawImages":
                            drawImages = bool.Parse(args[i + 1]);
                            break;
                        case "--graphFile":
                            graphFile = PathNormalizer.NormalizeFilePath(args[i + 1]);
                            break;
                        case "--labelFile":
                            labelFile = PathNormalizer.NormalizeFilePath(args[i + 1]);
                            break;
                        case "--listFile":
                            listFile = PathNormalizer.NormalizeFilePath(args[i + 1]);
                            break;
                        case "--logPath":
                            if (args[i + 1] != "undefined")
                            {
                                logPath = PathNormalizer.NormalizeFilePath(args[i + 1]);
                            }
                            else
                            {
                                logPath = "";
                            }
                            break;
                        case "--maxDetections":
                            maxDetections = int.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                            break;
                        case "--minScore":
                            minScore = float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                            break;
                        case "--modelDir":
                            modelDir = PathNormalizer.NormalizeDirectory(args[i + 1]);
                            break;
                        case "--outputDir":
                            outputDir = PathNormalizer.NormalizeDirectory(args[i + 1]);
                            break;
                    }
                }

                var config = new NLog.Config.LoggingConfiguration();

                // Targets where to log to: File and Console
                if (string.IsNullOrEmpty(logPath))
                {
                    logPath = "C:\\temp\\MachineLearningToolkit.log";
                }
                var logfile = new NLog.Targets.FileTarget("logfile") { FileName = logPath };

                // Rules for mapping loggers to targets            
                config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);

                // Apply config           
                NLog.LogManager.Configuration = config;
                if (args[0] == "ObjectDetection")
                {
                    try
                    {
                        ObjectDetection inference;

                        Log.Info("Iniciando a detecção de objetos.");

                        if (string.IsNullOrEmpty(graphFile) && string.IsNullOrEmpty(labelFile))
                        {
                            inference = new ObjectDetection(modelDir, maxDetections, minScore, outputDir);
                        }
                        else
                        {
                            inference = new ObjectDetection(modelDir, maxDetections, minScore, outputDir, graphFile, labelFile);
                        }

                        var results = inference.Inference(listFile, drawImages);

                        string outputFile = Path.Combine(outputDir, "Result.ObjectDetection");

                        JsonUtil<List<Result>>.WriteJsonOnFile(results, outputFile);

                        Log.Info("Detecção de objetos concluída.\n");

                        Console.WriteLine(outputFile);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Houve um erro na detecção de objetos: {ex.Message}");
                    }
                }
                else if (args[0] == "ImageClassification")
                {
                    try
                    {
                        ImageClassification inference;

                        Log.Info("Iniciando classificação de imagens");

                        if (string.IsNullOrEmpty(graphFile) && string.IsNullOrEmpty(labelFile))
                        {
                            inference = new ImageClassification(modelDir);
                        }
                        else
                        {
                            inference = new ImageClassification(modelDir, graphFile, labelFile);
                        }

                        var results = inference.Classify(listFile);

                        string outputFile = Path.Combine(outputDir, "Result.ImageClassification");

                        JsonUtil<List<ClassificationInference>>.WriteJsonOnFile(results, outputFile);

                        Log.Info("Classificação de imagens concluída.\n");

                        Console.WriteLine(outputFile);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Houve um erro na classificação de imagens: {ex.Message}");
                    }
                }
            }

            catch (IndexOutOfRangeException)
            {
                Log.Error("Verifique o comando executado");
            }
            catch (Exception ex)
            {
                Log.Error($"Erro interno: {ex.Message}");
            }
        }
    }
}