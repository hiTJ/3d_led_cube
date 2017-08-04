using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace sample
{
    class Program
    {
        [DllImport("ledLib32.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetUrl(string url);
        [DllImport("ledLib32.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetLed(int x, int y, int z, int color);
        [DllImport("ledLib32.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Clear();
        [DllImport("ledLib32.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Show();
        [DllImport("ledLib32.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Wait(int millisecond);
        [DllImport("ledLib32.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ShowMotioningText1(string text);
        [DllImport("ledLib32.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetChar(int x, int y, int z, char c, int color);
        [DllImport("ledLib32.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ShowFirework(int x, int y, int z);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// サンプリング周波数
        /// </summary>
        const int sampleRate = 44100;

        /// <summary>
        /// ステレオ(1)、モノラル(2)
        /// </summary>
        const int channels = 1;

        /// <summary>
        /// サンプリング点数
        /// </summary>
        const int samplePoint = 1024;

        /// <summary>
        /// x軸座標の点数（横）
        /// </summary>
        const int xAxisPoints = 16;

        /// <summary>
        /// y軸座標の点数（縦）
        /// </summary>
        const int yAxisPoints = 32;

        /// <summary>
        /// z軸座標の点数（奥行き）
        /// </summary>
        const int zAxisPoints = 8;

        /// <summary>
        /// サンプリングした点数を分割する値
        /// </summary>
        const int divider = 4;

        /// <summary>
        /// x軸のシフト値(大きいほど低周波をカットして高周波を増やす)
        /// </summary>
        const int xAxisShifter = 4;

        //係数は適当
        const int ampCoefficient = 20;

        static void Main(string[] args)
        {
            String test = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo libDir = Directory.GetParent(test).Parent.Parent.Parent.Parent;
            SetDllDirectory(Path.Combine(libDir.FullName, "00_lib"));
            //SetDllDirectory("00_lib");

            using (var waveIn = new WaveInEvent())
            {
                List<float> recorded = new List<float>();
                waveIn.WaveFormat = new WaveFormat(sampleRate: sampleRate, channels: channels);
                List<int[]> coordinates = new List<int[]>();

                waveIn.DataAvailable += (_, e) =>
                {
                    int[] coordinate = new int[xAxisPoints];
                    // 32bitで最大値1.0fにする
                    for (int index = 0; index < e.BytesRecorded; index += 2)
                    {
                        short sample = 0;
                        sample = (short)((e.Buffer[index + 1] << 8) | e.Buffer[index + 0]);

                        float sample32 = sample / 32768f;

                        recorded.Add(sample32);
                        if (recorded.Count == samplePoint)
                        {
                            var computed = ComputeFFT(recorded);
                            int value = 0;
                            int pointsPerBlock = samplePoint / xAxisPoints / divider;
                            int xAxisMax = computed.Count / divider + xAxisShifter * pointsPerBlock;
                            for (int i = xAxisShifter * pointsPerBlock; i < xAxisMax; i++)
                            {
                                value += (int)(computed[i]);
                                if (i % pointsPerBlock == 0 && i >= xAxisShifter * pointsPerBlock)
                                {
                                    if (value > pointsPerBlock * yAxisPoints)
                                    {
                                        value = pointsPerBlock * yAxisPoints;
                                    }
                                    int highestY = yAxisPoints - value / pointsPerBlock;
                                    //for (int y = yAxisPoints; y > highestY; y--)
                                    {
                                        //ここでLEDのパラメータをqueueに入れる
                                        coordinate[i / pointsPerBlock - xAxisShifter] = highestY;
                                    }
                                    value = 0;
                                }
                            }
                        }
                    }
                    coordinates.Add(coordinate);

                    for (int z = 0; z < coordinates.Count; z++)
                    {
                        int color = 0;
                        int[] xyCoordinate = coordinates[z];
                        for (int x = 0; x < xAxisPoints; x++)
                        {
                            for (int y = yAxisPoints; y >= xyCoordinate[x]; y--)
                            {
                                if (y < 4)
                                {
                                    color = 0xFF0000;
                                }
                                else if (4 <= y && y < 8)
                                {
                                    color = 0xFF7700;
                                }
                                else if (8 <= y && y < 12)
                                {
                                    color = 0x77FF00;
                                }
                                else if (16 <= y && y < 20)
                                {
                                    color = 0x00FF00;
                                }
                                else if (20 <= y && y < 24)
                                {
                                    color = 0x0077FF;
                                }
                                else if (24 <= y && y < 28)
                                {
                                    color = 0x0000FF;
                                }
                                else
                                {
                                    color = 0x000077;
                                }
                                SetLed(x, y, zAxisPoints - z, color);
                            }
                        }
                    }
                    if (coordinates.Count > zAxisPoints)
                    {
                        coordinates.RemoveAt(0);
                    }
                    Show();
                    Wait(10);
                    Clear();
                    recorded.Clear();
                };
                waveIn.StartRecording();
                Console.WriteLine("Press ENTER to quit...");
                Console.ReadLine();
                waveIn.StopRecording();
            }
            Console.WriteLine("Program is ended successfully.");
        }

        /// <summary>
        /// 音データにFFT処理を施し、FFT結果の絶対値を取得する(dBではない)
        /// </summary>
        /// <param name="record">音データ</param>
        /// <returns>FFT結果の絶対値</returns>
        private static List<double> ComputeFFT(List<float> record)
        {
            var window = Window.Hamming(samplePoint);
            List<float> converted = record.Select((v, i) => v * (float)window[i]).ToList();

            System.Numerics.Complex[] complexData = converted.Select(v => new System.Numerics.Complex(v, 0.0)).ToArray();

            Fourier.Forward(complexData, FourierOptions.Matlab); // arbitrary length

            List<double> magnitudes = new List<double>();
            foreach(System.Numerics.Complex comp in complexData)
            {
                magnitudes.Add(comp.Magnitude * ampCoefficient);
            }

            return magnitudes;
        }
    }
}
