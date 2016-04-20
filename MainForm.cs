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
using NAudio.Dsp;
using NAudio.Codecs;

namespace Sound_Editor {
    public enum Direction {
        BACK, FORWARD
    }

    public enum CompressionTypes {
        NONE, WAV, ALAW, MULAW
    }

    public partial class MainForm : Form {
        public MainForm() {
            InitializeComponent();
        }

        public static Position originalPosition = null;
        public static TimePeriod allocatedPeriod = null;
        public static TimePeriod viewPeriod = null;

        private List<AudioFile> files = new List<AudioFile>();
        private AudioFile currentAudio = null;
        private WaveOut output = null;
        private int currentAudioIndex = -1;
        private Direction direction;

        private WaveIn sourceStream = null;
        private WaveFileWriter waveWriter = null;
        private DateTime startRecordTime = new DateTime();
        private string fileToWrite;

        private void MainForm_Load(object sender, EventArgs e) {
            spectrumViewer.PenColor = Color.GreenYellow;
            spectrumViewer.PenWidth = 2;
            compressedSpectrumViewer.PenColor = Color.LawnGreen;
            compressedSpectrumViewer.PenWidth = 2;

            originalSpectrogramViewer.Area = originalVizualizationTab.TabPages[2];
            compressedSpectrogramViewer.Area = compressedVizualizationTab.TabPages[2];

            originalPosition = new Position(originalCurrentTime);
            originalPosition.CurrentTime = new TimeSpan(0);

            allocatedPeriod = new TimePeriod(timePeriods.Items[0]);
            allocatedPeriod.StartTime = new TimeSpan(0);
            allocatedPeriod.EndTime = new TimeSpan(0);

            viewPeriod = new TimePeriod(timePeriods.Items[2]);
            viewPeriod.StartTime = new TimeSpan(0);
            viewPeriod.EndTime = new TimeSpan(0);

            output = new WaveOut();
            output.Volume = 1f;
        }

        private void initAudioInfo() {
            audioNameLabel.Text = this.currentAudio.Name;
            audioFormatLabel.Text = this.currentAudio.Format;
            //audioCodecLabel.Text = 
            audioSizeLabel.Text = (this.currentAudio.Size * Math.Pow(10, 6)).ToString() + " bit";
            audioSampleRateInfo.Text = this.currentAudio.SampleRate.ToString();
            int index = audioBitDepthInfo.Items.IndexOf(this.currentAudio.bitDepth.ToString());
            if (index != -1) {
                audioBitDepthInfo.SelectedIndex = index;
            }
        }

        private void initAudio(AudioFile f) {
            this.currentAudio = f;

            originalSpectrogramViewer.Audio = f;

            originalWaveViewer.Spectrogram = originalSpectrogramViewer;
            originalWaveViewer.Audio = f;
            originalWaveViewer.FitToScreen();

            spectrumViewer.Audio = f;

            this.initAudioInfo();

            originalVizualizationTab.TabPages[0].Text = "Редактор: " + f.Name + "." + f.Format;
            audioRate.Text = f.SampleRate + " Hz";
            audioSize.Text = Math.Round(f.Size, 1).ToString() + " MB";
            audioLength.Text = Position.getTimeString(f.Duration);
            output.Init(f.Stream);

            //test();
        }

        private void initAudio(AudioFile f, bool compressed) {
            this.currentAudio = f;

            compressedSpectrogramViewer.Audio = f;

            compressedWaveViewer.Spectrogram = compressedSpectrogramViewer;
            compressedWaveViewer.Audio = f;
            compressedWaveViewer.FitToScreen();

            compressedSpectrumViewer.Audio = f;

            this.initAudioInfo();
            compressedVizualizationTab.TabPages[0].Text = "Редактор: " + f.Name + "." + f.Format;
            audioRate.Text = f.SampleRate + " Hz";
            audioSize.Text = Math.Round(f.Size, 1).ToString() + " MB";
            audioLength.Text = Position.getTimeString(f.Duration);
            output.Init(f.Stream);
        }

        private void test() {
            byte[] samples = new byte[this.currentAudio.ShortSamples.Length];
            for (int i = 0; i < this.currentAudio.ShortSamples.Length; i++) {
                samples[i] = ALawEncoder.LinearToALawSample(this.currentAudio.ShortSamples[i]);
            }

            WaveFormat format = new WaveFormat(8000, 8, 1);
            WaveFileWriter writer = new WaveFileWriter(@"D:\test2.wav", format);

            short[] buffer = new short[samples.Length];
            for (int i = 0; i < buffer.Length; i++) {
                buffer[i] = ALawDecoder.ALawToLinearSample(samples[i]);
            }

            writer.WriteData(samples, 0, samples.Length);
            writer.Close();
            MessageBox.Show("OK");
        }

        private void addFileToListView(AudioFile f) {
            ListViewItem item = new ListViewItem(f.Name);
            item.SubItems.Add(Position.getTimeString(f.Duration));
            item.SubItems.Add(f.SampleRate.ToString() + " Hz");
            item.SubItems.Add(f.Format.ToString());
            item.SubItems.Add(f.Path.ToString());
            item.SubItems.Add(f.bitDepth.ToString() + " bit");
            Color color;
            switch (f.Compression) {
                case CompressionTypes.WAV: color = Color.Green; break;
                default: color = Color.Black; break;
            }
            item.ForeColor = color;
            listAudio.Items.Add(item);
        }

        private void addFileToListView(string path) {
            string name, format;
            AudioFile.getNameAndFormatFromPath(path, out name, out format);
            ListViewItem item = new ListViewItem(name);
            item.SubItems.Add("00:00:000");
            item.SubItems.Add("44100 Hz");
            item.SubItems.Add(format);
            item.SubItems.Add(path);
            item.SubItems.Add("16 bit");
            item.ForeColor = Color.Blue;
            listAudio.Items.Add(item);
        }

        private void openToolStripButton_Click(object sender, EventArgs e) {
            AudioFile file = null;
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "Audio File (*.mp3;*.wav)|*.mp3;*.wav;";
            if (open.ShowDialog() != DialogResult.OK) return;
            AudioFile searchFile = files.Find(x => x.Path == open.FileName);
            if (searchFile != null) {
                MessageBox.Show("Этот файл уже добавлен в список.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (open.FileName.EndsWith(".mp3")) {
                Mp3FileReader reader = new Mp3FileReader(open.FileName);
                WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(reader);
                BlockAlignReductionStream stream = new BlockAlignReductionStream(pcm);
                file = new MP3File(reader, stream, open.FileName);
            } else if (open.FileName.EndsWith(".wav")) {
                WaveFileReader reader = new WaveFileReader(open.FileName);
                WaveStream pcm = new WaveChannel32(reader);
                BlockAlignReductionStream stream = new BlockAlignReductionStream(pcm);
                file = new WaveFile(reader, stream, open.FileName);
            } else throw new InvalidOperationException("Неверный формат аудиофайла");
            files.Add(file);
            this.addFileToListView(file);
            if (files.Count == 1) {
                this.currentAudioIndex = 0;
                this.initAudio(file);
            }
        }

        private void listAudio_MouseDoubleClick(object sender, MouseEventArgs e) {
            if (listAudio.SelectedItems.Count == 0) return;
            if (listAudio.SelectedItems[0].ForeColor == Color.Blue) {
                MessageBox.Show("Запишите данные в файл, перед тем как открыть.", "Пустой файл", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (this.currentAudio != null && listAudio.SelectedItems[0].Text == this.currentAudio.Name) return;
            AudioFile file = files.Find(audio => audio.Path == listAudio.SelectedItems[0].SubItems[4].Text);
            if (output.PlaybackState == PlaybackState.Playing) {
                output.Pause();
                audioStatus.Text = "Приостановлено: " + currentAudio.Name + "." + currentAudio.Format;
            }
            this.currentAudioIndex = listAudio.SelectedItems[0].Index;
            this.initAudio(file);
        }

        private void deleteToolStripButton_Click(object sender, EventArgs e) {
            if (listAudio.SelectedItems.Count == 0) return;
            AudioFile file = files.Find(audio => audio.Path == listAudio.SelectedItems[0].SubItems[4].Text);
            listAudio.Items.Remove(listAudio.SelectedItems[0]);
            if (file == null) return;
            if (this.currentAudio == file) {
                if (this.currentAudio.Compression == CompressionTypes.NONE) {
                    if (output.PlaybackState == PlaybackState.Playing) {
                        originalPlayTimer.Stop();
                        output.Stop();
                        originalCurrentTime.Text = "00:00:000";
                    }
                    currentAudio = null;
                    originalWaveViewer.WaveStream = null;
                    spectrumViewer.Audio = null;
                    originalSpectrogramViewer.Count = 0;
                } else {
                    if (output.PlaybackState == PlaybackState.Playing) {
                        //originalPlayTimer.Stop();
                        output.Stop();
                        //originalCurrentTime.Text = "00:00:000";
                    }
                    currentAudio = null;
                    compressedWaveViewer.WaveStream = null;
                    compressedSpectrumViewer.Audio = null;
                    compressedSpectrogramViewer.Count = 0;
                }
            }
            if (file.Format == "mp3") {
                MP3File deleteFile = file as MP3File;
                deleteFile.Reader.Dispose();
            } else if (file.Format == "wav") {
                WaveFile deleteFile = file as WaveFile;
                deleteFile.Reader.Dispose();
            }
            currentAudioIndex = -1;
            file.Stream.Dispose();
            files.Remove(file);
            file = null;
        }

        // Pause
        private void toolStripButton3_Click(object sender, EventArgs e) {
            if (output != null && currentAudio != null) {
                if (output.PlaybackState == PlaybackState.Playing) {
                    output.Pause();
                    originalPlayTimer.Enabled = false;
                    spectrumTimer.Enabled = false;
                    audioStatus.Text = "Приостановлено: " + currentAudio.Name + "." + currentAudio.Format;
                }
            }
        }

        // Play
        private void toolStripButton1_Click(object sender, EventArgs e) {
            if (output != null && currentAudio != null) {
                if (output.PlaybackState != PlaybackState.Playing) {
                    output.Play();
                    originalPlayTimer.Enabled = true;
                    spectrumTimer.Enabled = true;
                    audioStatus.Text = "Воспроизведение: " + currentAudio.Name + "." + currentAudio.Format;
                }
            }
        }

        // Stop
        private void toolStripButton2_Click(object sender, EventArgs e) {
            if (output != null && currentAudio != null) {
                if (output.PlaybackState != PlaybackState.Stopped) {
                    currentAudio.Stream.Position = 0;
                    output.Stop();
                    originalPlayTimer.Enabled = false;
                    originalPosition.CurrentTime = new TimeSpan(0);
                    spectrumTimer.Enabled = false;
                    audioStatus.Text = "Остановлено: " + currentAudio.Name + "." + currentAudio.Format;
                }
            }
        }

        // Previous audio
        private void toolStripButton4_Click(object sender, EventArgs e) {
            if (output != null && this.files.Count > 1) {
                this.listAudio.Items[this.currentAudioIndex].Selected = false;
                if (this.currentAudioIndex == 0) {
                    this.currentAudioIndex = this.listAudio.Items.Count;
                }
                this.listAudio.Items[this.currentAudioIndex - 1].Selected = true;
                this.listAudio_MouseDoubleClick(sender, new MouseEventArgs(MouseButtons.Left, 2, 0, 0, 0));
            }
        }

        // Next audio
        private void toolStripButton7_Click(object sender, EventArgs e) {
            if (output != null && this.files.Count > 1) {
                this.listAudio.Items[this.currentAudioIndex].Selected = false;
                if (this.currentAudioIndex == this.listAudio.Items.Count - 1) {
                    this.currentAudioIndex = -1;
                }
                this.listAudio.Items[this.currentAudioIndex + 1].Selected = true;
                this.listAudio_MouseDoubleClick(sender, new MouseEventArgs(MouseButtons.Left, 2, 0, 0, 0));
            }
        }

        /* Методы перемотки */

        // Back
        private void back() {
            long position = this.currentAudio.Stream.Position;
            if (currentAudio.Format == "mp3") {
                MP3File file = currentAudio as MP3File;
                position = file.Reader.Position;
            }
            position = position - this.currentAudio.Stream.WaveFormat.AverageBytesPerSecond;
            if (position < 0) {
                position = 0;
                this.currentAudio.Stream.CurrentTime = new TimeSpan(0);
            } else {
                this.currentAudio.Stream.CurrentTime.Subtract(new TimeSpan(0, 0, 1));
            }
            this.currentAudio.Stream.Position = position;
            if (output.PlaybackState != PlaybackState.Playing) {
                originalPosition.CurrentTime = this.currentAudio.Stream.CurrentTime;
            }
        }

        private void toolStripButton5_MouseDown(object sender, MouseEventArgs e) {
            if (output != null && this.currentAudio != null) {
                this.direction = Direction.BACK;
                changePositionTimer.Start();
            }
        }

        private void toolStripButton5_MouseUp(object sender, MouseEventArgs e) {
            changePositionTimer.Stop();
        }

        // Forward
        private void forward() {
            long position = this.currentAudio.Stream.Position;
            if (currentAudio.Format == "mp3") {
                MP3File file = currentAudio as MP3File;
                position = file.Reader.Position;
            }
            if (position + this.currentAudio.Stream.WaveFormat.AverageBytesPerSecond > this.currentAudio.Stream.Length) return;
            this.currentAudio.Stream.Position = position + this.currentAudio.Stream.WaveFormat.AverageBytesPerSecond;
            this.currentAudio.Stream.CurrentTime.Add(new TimeSpan(0, 0, 1));
            if (output.PlaybackState != PlaybackState.Playing) {
                originalPosition.CurrentTime = this.currentAudio.Stream.CurrentTime;
            }
        }

        private void toolStripButton6_MouseDown(object sender, MouseEventArgs e) {
            if (output != null && this.currentAudio != null) {
                this.direction = Direction.FORWARD;
                changePositionTimer.Start();
            }
        }

        private void toolStripButton6_MouseUp(object sender, MouseEventArgs e) {
            changePositionTimer.Stop();
        }

        private void changePositionTimer_Tick(object sender, EventArgs e) {
            if (this.direction == Direction.BACK) {
                this.back();
            } else if (this.direction == Direction.FORWARD) {
                this.forward();
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
            if (currentAudio.Stream != null) {
                currentAudio.Stream.Dispose();
                currentAudio.Stream = null;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            //DisposeWave();
        }

        private void originalPlayTimer_Tick(object sender, EventArgs e) {
            if (currentAudio == null) return;
            TimeSpan currentTime = new TimeSpan();
            if (currentAudio.Format == "mp3") {
                MP3File file = currentAudio as MP3File;
                currentTime = file.Reader.CurrentTime;
            } else if (currentAudio.Format == "wav") {
                WaveFile file = currentAudio as WaveFile;
                currentTime = file.Reader.CurrentTime;
            }
            if (currentAudio.Duration == currentTime) {
                toolStripButton2_Click(sender, e);
            }

            originalPosition.CurrentTime = currentTime;
        }

        private void trackBarOriginal_Scroll(object sender, EventArgs e) {
            if (output != null) {
                output.Volume = trackBarOriginal.Value / 10f;
            }
        }

        private void spectrumTimer_Tick(object sender, EventArgs e) {
            spectrumViewer.Refresh();
        }

        /* Методы обработки входного сигнала */

        // Создание пустого wav файла на диске
        private void newToolStripButton_Click(object sender, EventArgs e) {
            SaveFileDialog save = new SaveFileDialog();
            save.Filter = "Wave File (*.wav)|*.wav;";
            if (save.ShowDialog() != DialogResult.OK) return;
            this.addFileToListView(save.FileName);
        }

        // Обновление списка доступных записывающих устройств
        private void refreshDeviceListButton_Click(object sender, EventArgs e) {
            List<WaveInCapabilities> sources = new List<WaveInCapabilities>();
            for (int i = 0; i < WaveIn.DeviceCount; i++) {
                sources.Add(WaveIn.GetCapabilities(i));
            }
            devicesListView.Items.Clear();
            foreach (var source in sources) {
                ListViewItem item = new ListViewItem(source.ProductName);
                item.SubItems.Add(new ListViewItem.ListViewSubItem(item, source.Channels.ToString()));
                devicesListView.Items.Add(item);
            }
        }

        private int selectedItemToWrite;

        private void startRecordButton_Click(object sender, EventArgs e) {
            try {
                if (devicesListView.SelectedItems.Count == 0) {
                    throw new Exception("Вы не выбрали записывающее устройство.");
                }
                if (listAudio.SelectedItems.Count == 0) {
                    throw new Exception("Вы не выбрали файл для записи. Выделите нужный вам файл в списке аудио.");
                }
                if (listAudio.SelectedItems[0].ForeColor != Color.Blue) {
                    throw new Exception("Запись в этот файл невозможна.");
                }
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            startRecordButton.Enabled = false;
            this.selectedItemToWrite = listAudio.SelectedItems[0].Index;
            this.fileToWrite = listAudio.SelectedItems[0].SubItems[4].Text;
            int deviceNumber = devicesListView.SelectedItems[0].Index;
            this.sourceStream = new WaveIn();
            this.sourceStream.DeviceNumber = deviceNumber;
            this.sourceStream.WaveFormat = new WaveFormat(44100, 16, WaveIn.GetCapabilities(deviceNumber).Channels);

            this.sourceStream.DataAvailable += new EventHandler<WaveInEventArgs>(SourceStream_DataAvailable);
            this.waveWriter = new WaveFileWriter(this.fileToWrite, this.sourceStream.WaveFormat);

            this.sourceStream.StartRecording();
            this.startRecordTime = DateTime.Now;
            recordingTimer.Start();
        }

        private void SourceStream_DataAvailable(object sender, WaveInEventArgs e) {
            if (this.waveWriter == null) return;
            this.waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
            this.waveWriter.Flush();
        }

        private void stopRecordButton_Click(object sender, EventArgs e) {
            recordingTimer.Stop();
            startRecordButton.Enabled = true;
            if (this.sourceStream != null) {
                this.sourceStream.StopRecording();
                this.sourceStream.Dispose();
                this.sourceStream = null;
            }
            if (this.waveWriter != null) {
                this.waveWriter.Dispose();
                this.waveWriter = null;
                this.openSavedFile();
            }
        }

        private void openSavedFile() {
            WaveFileReader reader = new WaveFileReader(this.fileToWrite);
            WaveStream pcm = new WaveChannel32(reader);
            BlockAlignReductionStream stream = new BlockAlignReductionStream(pcm);
            AudioFile file = new WaveFile(reader, stream, this.fileToWrite);
            this.files.Add(file);
            listAudio.Items[selectedItemToWrite].SubItems[1].Text = Position.getTimeString(file.Duration);
            listAudio.Items[selectedItemToWrite].ForeColor = Color.Black;
            if (files.Count == 1) {
                MessageBox.Show("Аудиозапись успешно сохранена.", "Записано", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.initAudio(file);
            } else {
                DialogResult result = MessageBox.Show("Аудиозапись успешно сохранена. Открыть запись?", "Записано", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes) {
                    this.initAudio(file);
                }
            }
            recordTimerLabel.Text = "00:00:000";
        }

        private void recordingTimer_Tick(object sender, EventArgs e) {
            DateTime dtn = DateTime.Now;
            TimeSpan ts = dtn.Subtract(this.startRecordTime);
            recordTimerLabel.Text = Position.getTimeString(ts);
        }

        private void saveWavButton_Click(object sender, EventArgs e) {
            if (this.currentAudio == null) return;
            SaveFileDialog save = new SaveFileDialog();
            save.Filter = "Wave File (*.wav)|*.wav;";
            if (save.ShowDialog() != DialogResult.OK) return;

            WaveFormat format = new WaveFormat(int.Parse(audioSampleRateInfo.Text), int.Parse(audioBitDepthInfo.Text), 2);
            WaveFormatConversionStream convertedStream = null;
            if (this.currentAudio.Format == "mp3") {
                MP3File afile = this.currentAudio as MP3File;
                convertedStream = new WaveFormatConversionStream(format, afile.Reader);
            } else {
                WaveFile afile = this.currentAudio as WaveFile;
                convertedStream = new WaveFormatConversionStream(format, afile.Reader);
            }
            WaveFileWriter.CreateWaveFile(save.FileName, convertedStream);
            DialogResult dres = MessageBox.Show("Аудиофайл успешно сохранен. Открыть файл?", "Файл сохранен", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dres == DialogResult.Yes) {
                WaveFileReader reader = new WaveFileReader(save.FileName);
                WaveStream pcm = new WaveChannel32(reader);
                BlockAlignReductionStream stream = new BlockAlignReductionStream(pcm);
                AudioFile file = new WaveFile(reader, stream, save.FileName);
                file.Compression = CompressionTypes.WAV;
                this.files.Add(file);
                this.addFileToListView(file);
                this.initAudio(file, true);
            }
        }
    }
}
