﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using NAudio.Dsp;

namespace Sound_Editor {
    public class SpectrogramViewer : System.Windows.Forms.UserControl {
        public long StartPosition { get; set; }
        public TabPage Area { get; set; }
        public AudioFile Audio { get; set; }
        private Bitmap bitMap;
        private int count;
        public int Count {
            get {
                return count;
            }
            set {
                if (this.Area != null) {
                    this.count = value;
                    this.Area.AutoScrollMinSize = new Size(this.count, 512);
                    this.Width = this.count;
                    this.bitMap = null;
                }
            }
        }

        private System.ComponentModel.Container components = null;

        public SpectrogramViewer() {
            InitializeComponent();
            this.DoubleBuffered = true;
        }

        private void drawBitMap() {
            this.bitMap = new Bitmap(this.count, 512);
            long position = 0;
            double max = 0;
            for (int i = 0; i < this.Audio.FloatSamples.Length / 1024; i++) {
                double[] spectrum = SpectrumViewer.getSpectrum(this.Audio, position);
                position += 1024;

                for (int j = 0; j < spectrum.Length; j++) {
                    if (spectrum[j] > max) {
                        max = spectrum[j];
                    }
                }
            }
            position = this.StartPosition;
            double koef; int x = 0;
            for (int i = 0; i < this.count; i++, x++) {
                double[] spectrum = SpectrumViewer.getSpectrum(this.Audio, position);
                position += 1024;
                koef = 175 / max * 8;
                int color;
                for (int j = 0; j < spectrum.Length; j++) {
                    color = 80 + (int)(spectrum[j] * koef);
                    if (color > 255) color = 255;
                    this.bitMap.SetPixel(x, 512 - j - 1, Color.FromArgb(0, color, 0));
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            if (this.Audio != null) {
                if (this.bitMap == null) {
                    this.drawBitMap();
                }
                e.Graphics.DrawImage(this.bitMap, new PointF(0, 0));
            }
            base.OnPaint(e);
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                if (components != null) {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
        }
        #endregion
    }
}
