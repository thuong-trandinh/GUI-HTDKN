using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Deployment.Application;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using ZedGraph;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;
using System.Diagnostics;

namespace BTL_DLCN
{
    public partial class Form1 : Form
    {
        private bool isDrawing = false;
        private Stopwatch _stopwatch;
        private const double WindowWidthSec = 10.0;   // hiển thị 10 giây gần nhất

        // Bộ đệm chứa byte đang chờ xử lý
        private List<byte> rxBuffer = new List<byte>();

        // Lưu mảng float của lần frame cuối cùng hợp lệ
        private float[] lastValidValues = new float[0];
        private ushort ComputeCRC16(byte[] data, int offset, int count)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < count; i++)
            {
                crc ^= (ushort)(data[offset + i] << 8);
                for (int b = 0; b < 8; b++)
                    crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
            }
            return crc;
        }
        private void ProcessRxBuffer()
        {
            lock (rxBuffer)
            {
                while (true)
                {
                    // 1) cần tối thiểu 7 byte để có start,cmd,len(2),CRC(2),end
                    if (rxBuffer.Count < 7) break;

                    // 2) tìm start-byte = 0xAA
                    int s = rxBuffer.IndexOf(0xAA);
                    if (s < 0)
                    {
                        rxBuffer.Clear();
                        break;
                    }
                    // nếu start không ở đầu, vứt hết trước start
                    if (s > 0)
                        rxBuffer.RemoveRange(0, s);

                    // 3) đọc length (2 byte) nếu chưa đủ thì chờ thêm
                    if (rxBuffer.Count < 4) break;
                    ushort length = (ushort)((rxBuffer[2] << 8) | rxBuffer[3]);
                    int frameSize = 7 + length;
                    if (rxBuffer.Count < frameSize) break;

                    // 4) kiểm tra end-byte
                    if (rxBuffer[frameSize - 1] != 0xAF)
                    {
                        // lỗi frame, loại bỏ start này và thử lại
                        rxBuffer.RemoveAt(0);
                        continue;
                    }

                    // 5) tách frame ra mảng riêng
                    byte[] frame = rxBuffer.GetRange(0, frameSize).ToArray();
                    rxBuffer.RemoveRange(0, frameSize);

                    // 6) kiểm tra CRC (2 byte trước end)
                    ushort receivedCrc = (ushort)((frame[4 + length] << 8) | frame[5 + length]);
                    ushort calcCrc = ComputeCRC16(frame, 1, length + 3);
                    if (receivedCrc == calcCrc && frame[1] == 0x20)
                    {
                        // giải mã payload thành float[]
                        int nFloats = length / 4;
                        float[] values = new float[nFloats];
                        for (int i = 0; i < nFloats; i++)
                            values[i] = BitConverter.ToSingle(frame, 4 + i * 4);

                        lastValidValues = values;           // lưu mảng mới
                        DrawValues(values);                  // vẽ ngay
                    }
                    else
                    {
                        // CRC sai → vẽ lại mảng cũ (nếu có)
                        if (lastValidValues.Length > 0)
                            DrawValues(lastValidValues);
                    }
                }
            }
        }

        // Vẽ mảng giá trị lên ZedGraph (giả sử mỗi frame chỉ vẽ 1 điểm của giá trị đầu)
        private void DrawValues(float[] vals)
        {
            if (!isDrawing) return;
            // ví dụ chỉ vẽ vals[0], vals[1] … theo 2 curve, hoặc vẽ tất cả
            for (int i = 0; i < vals.Length; i++)
            {
                draw(vals[i]);   // dùng hàm bạn có sẵn để thêm 1 điểm
            }
        }
        public Form1()
        {
            InitializeComponent();
            _stopwatch = Stopwatch.StartNew();
        }
        string[] baud = { "1200", "2400", "4800", "9600", "14400", "19200", "38400", "56000", "57600", "115200" };
        private void Form1_Load(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            CBoxPortName.Items.AddRange(ports);

            //Init graphPane
            GraphPane mypane1 = zedGraphControl1.GraphPane;
            

            mypane1.Title.Text = "Chart";
            mypane1.XAxis.Title.Text = "Time";
            mypane1.YAxis.Title.Text = "Temprature";
            RollingPointPairList list1 = new RollingPointPairList(60000);
            LineItem line1 = mypane1.AddCurve("Temprature", list1, Color.Red, SymbolType.Diamond);
           
            
            

            mypane1.XAxis.Scale.MinAuto = true;
            mypane1.XAxis.Scale.MaxAuto = true;
            mypane1.YAxis.Scale.MinAuto = true;
            mypane1.YAxis.Scale.MaxAuto = true;
            mypane1.YAxis.Scale.MinorStep = 1;
            mypane1.YAxis.Scale.MajorStep = 5;

            zedGraphControl1.AxisChange();

            string[] myport = SerialPort.GetPortNames();
            CBoxPortName.Items.AddRange(myport);
            CBoxBuadrate.Items.AddRange(baud);

        }
        uint tong = 0;
        public void draw(double value)
        {
            if (!isDrawing) return;// neu chua bam start thi khong ve

            var pane = zedGraphControl1.GraphPane;
            LineItem curve = pane.CurveList[0] as LineItem;
            if (curve == null) return;

            var list = curve.Points as IPointListEdit;
            if (list == null) return;

            // X = thời gian thực (giây) kể từ lúc bắt đầu
            double t = _stopwatch.Elapsed.TotalSeconds;
            list.Add(t, value);

            double maxX = t;
            double minX = maxX - 0.05;   // Hiển thị khoảng 2.5 chu kỳ (0.05s ~ 2.5 chu kỳ 50Hz)
            if (minX < 0) minX = 0;

            pane.XAxis.Scale.Min = minX;
            pane.XAxis.Scale.Max = maxX;

            pane.AxisChange();
            zedGraphControl1.Invalidate();
        }
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try 
            {
                if (!serialPort1.IsOpen)
                {
                    btnConnect.Text = "Disconnect";
                    serialPort1.PortName = CBoxPortName.Text;
                    serialPort1.BaudRate = Convert.ToInt32(CBoxBuadrate.Text);

                    serialPort1.Open();
                }
                else
                {
                    btnConnect.Text = "Connect";
                    serialPort1.Close();
                }
            } catch (Exception ex) 
            {
                MessageBox.Show("Lỗi kết nối: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                
                    string dulieu = txtSend.Text;
                    serialPort1.Write(dulieu);
                
            }
            catch
            {
                MessageBox.Show("Error", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int n = serialPort1.BytesToRead;
            byte[] buf = new byte[n];
            serialPort1.Read(buf, 0, n);

            lock (rxBuffer)
            {
                rxBuffer.AddRange(buf);
            }

            // Sang UI thread để cập nhật chart
            BeginInvoke(new Action(ProcessRxBuffer));
        }

        private void btnTimeSample_Click(object sender, EventArgs e)
        {
            try
            {
                if (!uint.TryParse(txtSampleTime.Text, out uint sampleTime) || sampleTime > 100000)
                {
                    MessageBox.Show("Giá trị không hợp lệ!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // Gửi dữ liệu qua SerialPort
                if (serialPort1.IsOpen)
                {
                    string temp = sampleTime.ToString();
                    serialPort1.Write(temp + "\r\n");
                }
                else
                {
                    MessageBox.Show("Vui lòng kết nối cổng serial trước!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            isDrawing = true;
            _stopwatch.Restart();   // reset thời gian về 0 khi Start

            // --- XÓA HẾT DỮ LIỆU CŨ ---
            var pane = zedGraphControl1.GraphPane;
            foreach (CurveItem curve in pane.CurveList)
            {
                if (curve.Points is IPointListEdit list)
                {
                    list.Clear();
                }
            }
            zedGraphControl1.Invalidate();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            isDrawing = false;
        }
    }
}


