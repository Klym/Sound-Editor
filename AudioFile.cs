﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NAudio.Wave;

namespace Sound_Editor {
    public abstract class AudioFile {
        public string Name { get; set; }
        public TimeSpan Duration { get; set; }
        public int SampleRate { get; set; }
        public int bitDepth { get; set; }
        public double Size { get; set; }
        public string Format { get; set; }
        public string Path { get; set; }
        public byte[] Samples { get; set; }
        public float[] FloatSamples { get; set; }
        public float Avg { get; set; }
        public BlockAlignReductionStream Stream { get; set; }

        public AudioFile(BlockAlignReductionStream stream, string path) {
            int startIndexOfName = path.LastIndexOf('\\') + 1;
            int startIndexOfFormat = path.LastIndexOf('.') + 1;
            int nameLength = startIndexOfFormat - startIndexOfName - 1;

            this.Name = path.Substring(startIndexOfName, nameLength);
            this.Duration = stream.TotalTime;
            this.SampleRate = stream.WaveFormat.SampleRate;
            this.bitDepth = stream.WaveFormat.BitsPerSample;
            this.Size = new FileInfo(path).Length * Math.Pow(10, -6);
            this.Format = path.Substring(startIndexOfFormat);
            this.Path = path;
            this.Stream = stream;
        }

        protected void readFloats() {
            AudioFileReader r = new AudioFileReader(this.Path);
            long sampleCount = r.Length / 4;
            this.FloatSamples = new float[sampleCount];
            r.Read(this.FloatSamples, 0, (int)sampleCount);
            this.getAvg();
        }

        private void getAvg() {
            this.Avg = 0;
            for (int i = 0; i < this.FloatSamples.Length; i++) {
                if (this.Avg < this.FloatSamples[i]) {
                    this.Avg = this.FloatSamples[i];
                }
            }
            this.Avg /= 64;
        }
    }
}
