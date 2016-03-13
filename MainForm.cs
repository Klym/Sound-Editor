﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio;
using NAudio.Wave;

namespace Sound_Editor {
    public partial class MainForm : Form {
        public MainForm() {
            InitializeComponent();
        }

        private List<AudioFile> files = new List<AudioFile>();
        private BlockAlignReductionStream currentStream = null;
        private DirectSoundOut output = null;

        private void openToolStripButton_Click(object sender, EventArgs e) {
            BlockAlignReductionStream stream = null;
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "Audio File (*.mp3;*.wav)|*.mp3;*.wav;";
            if (open.ShowDialog() != DialogResult.OK) return;
            if (open.FileName.EndsWith(".mp3")) {
                WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(new Mp3FileReader(open.FileName));
                stream = new BlockAlignReductionStream(pcm);
            } else if (open.FileName.EndsWith(".wav")) {
                WaveStream pcm = new WaveChannel32(new WaveFileReader(open.FileName));
                stream = new BlockAlignReductionStream(pcm);
            } else throw new InvalidOperationException("Неверный формат аудиофайла");
            AudioFile file = new AudioFile(stream, open.FileName);
            files.Add(file);
            ListViewItem item = new ListViewItem(file.Name);
            item.SubItems.Add(String.Format("{0:00}:{1:00}:{2:000}", file.Duration.Minutes, file.Duration.Seconds, file.Duration.Milliseconds));
            item.SubItems.Add(file.SampleRate.ToString() + " Hz");
            item.SubItems.Add(file.Format.ToString());
            item.SubItems.Add(file.Path.ToString());
            item.SubItems.Add(file.bitDepth.ToString() + " bit");
            listAudio.Items.Add(item);
            if (files.Count == 1) {
                currentStream = stream;
                originalWaveViewer.WaveStream = stream;
                originalWaveViewer.FitToScreen();

                output = new DirectSoundOut();
                output.Init(stream);

                originalPlayTimer.Interval = 1;
            }
        }

        private void редактироватьToolStripMenuItem_Click(object sender, EventArgs e) {
            if (listAudio.SelectedItems.Count > 0) {
                AudioFile file = files.Find(audio => audio.Name == listAudio.SelectedItems[0].Text && audio.Format == listAudio.SelectedItems[0].SubItems[3].Text);
                if (output.PlaybackState == PlaybackState.Playing) {
                    output.Pause();
                }
                output.Init(file.Stream);
            } else {
                MessageBox.Show("Вы не выбрали аудио файл.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Pause
        private void toolStripButton3_Click(object sender, EventArgs e) {
            if (output != null) {
                if (output.PlaybackState == PlaybackState.Playing) {
                    output.Pause();
                }
            }
        }

        // Play
        private void toolStripButton1_Click(object sender, EventArgs e) {
            if (output != null) {
                if (output.PlaybackState != PlaybackState.Playing) {
                    output.Play();
                    originalPlayTimer.Enabled = true;
                }
            }
        }

        // Stop
        private void toolStripButton2_Click(object sender, EventArgs e) {
            if (output != null) {
                if (output.PlaybackState != PlaybackState.Stopped) {
                    currentStream.Position = 0;
                    output.Stop();
                    originalCurrentTime.Text = "00:00:000";
                    originalPlayTimer.Enabled = false;
                }
            }
        }

        private void DisposeWave() {
            if (output != null) {
                if (output.PlaybackState == PlaybackState.Playing) {
                    output.Stop();
                }
                output.Dispose();
                output = null;
            }
            if (currentStream != null) {
                currentStream.Dispose();
                currentStream = null;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            DisposeWave();
        }

        private void originalPlayTimer_Tick(object sender, EventArgs e) {
            originalCurrentTime.Text = String.Format("{0:00}:{1:00}:{2:000}", currentStream.CurrentTime.Minutes, currentStream.CurrentTime.Seconds, currentStream.CurrentTime.Milliseconds);
        }
    }
}
