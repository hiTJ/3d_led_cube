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


        const int samplePoint = 1024;
        const int xAxisPoints = 16;
        const int yAxisPoints = 32;
        const int zAxisPoints = 8;

        static void Main(string[] args)
        {
            String test = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo libDir = Directory.GetParent(test).Parent.Parent.Parent.Parent;
            SetDllDirectory(Path.Combine(libDir.FullName,"00_lib"));


            using (var waveIn = new WaveInEvent())
            {
                List<float> recorded = new List<float>();
                waveIn.WaveFormat = new WaveFormat(sampleRate: 44100, channels: 1);

                waveIn.DataAvailable += (_, e) =>
                {
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
                            for (int i = 0; i < computed.Count / 2; i++)
                            {
                                int blockPoints = samplePoint / xAxisPoints / 2;
                                value += (int)(computed[i]);
                                if (i % blockPoints == 0 && i != 0)
                                {
                                    for (int z = 0; z < zAxisPoints; z++)
                                    {
                                        if (value > blockPoints * 32)
                                        {
                                            value = blockPoints * 32;
                                        }
                                        int highestY = 32 - value / blockPoints;
                                        for(int y = yAxisPoints; y > highestY; y--)
                                        {
                                            int color = 0;
                                            if(y < 10)
                                            {
                                                color = 0xFF0000;
                                            }
                                            else
                                            {
                                                color = 0x00FF00;
                                            }
                                            SetLed(i / blockPoints, y, z, color);
                                        }
                                    }
                                    Show();
                                    value = 0;
                                }
                            }
                            Clear();
                            recorded.Clear();
                        }
                    }
                };
                waveIn.StartRecording();
                Console.WriteLine("Press ENTER to quit...");
                Console.ReadLine();
                waveIn.StopRecording();
            }
            Console.WriteLine("Program is ended successfully.");
        }

        private static List<double> ComputeFFT(List<float> record)
        {
            var window = Window.Hamming(samplePoint);
            List<float> converted = record.Select((v, i) => v * (float)window[i]).ToList();

            System.Numerics.Complex[] complexData = converted.Select(v => new System.Numerics.Complex(v, 0.0)).ToArray();

            Fourier.Forward(complexData, FourierOptions.Matlab); // arbitrary length

            List<double> magnitudes = new List<double>();
            foreach(System.Numerics.Complex comp in complexData)
            {
                //magnitudes.Add(System.Math.Log(comp.Magnitude) * 20 + 100);
                magnitudes.Add(comp.Magnitude * 20);
            }

            return magnitudes;
        }

        private static void ShowLed(float sample)
        {

        }
    }
}
