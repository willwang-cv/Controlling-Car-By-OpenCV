/*
 * 最新更新于:2022/3/22 更新内容:添加更详细的注释
 */

using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace AI.小车控制台
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            cap.Open(0); //camera
            if (!cap.IsOpened()) return; // retval
            trackBar1.Maximum = 255; //trackBar1
            bitmap = new Bitmap(pictureBox4.Width, pictureBox4.Height);
            g = Graphics.FromImage(bitmap);
            numericUpDown1.Minimum = 1;
            numericUpDown1.Maximum = 99;
            a[0] = "停止";
            backgroundFrameMatIsCaptured = false;
            timer1.Start();
            numericUpDown2.Minimum = 1;
            numericUpDown2.Value = gaussianBlurLevel;
        }

        private static VideoCapture cap = new VideoCapture();//init VideoCapture
        private Mat m = new Mat();
        private OpenCvSharp.Point[][] contours;
        private HierarchyIndex[] hierarchly;
        private Scalar color_orange = new Scalar(100, 150, 255);
        private Scalar color_green = new Scalar(144, 238, 144);
        private Scalar color_yellow = new Scalar(0, 255, 255);
        private Scalar color_blue = new Scalar(255, 0, 0);
        private System.Drawing.Point targetPoint;
        private Socket client;
        private int erodeLevel = 1, threshold_value = 0, gaussianBlurLevel = 3;
        private int minVal = 200, maxVal = 255;
        private Mat backgroundFrameMat = new Mat();
        private bool backgroundFrameMatIsCaptured;
        private Mat subtracted_mat = new Mat();
        private Mat liveShow_image = new Mat();
        private RotatedRect Rect_only;
        private double k, b, subtraction_result;
        private int j = 1;
        private string[] a = new string[65535];
        private CircleSegment[] circleSegments = null; //circle structure retrieved from cvHoughCircle

        private void Timer1_Tick(object sender, EventArgs e) // Timer定时器 每隔10ms刷新
        {
            m = cap.RetrieveMat();//retrieve a mat from camera
            pictureBox1.Image = m.ToBitmap();//pictureBox1(显示原视频)
            if (backgroundFrameMatIsCaptured)
            {
                //帧差法 消除背景信息、噪声等对后续图像处理的影响
                Cv2.Absdiff(backgroundFrameMat, m, subtracted_mat);
                //通过GetStructuringElement()方法生成腐蚀操作时所采用的结构类型
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(erodeLevel, erodeLevel), new OpenCvSharp.Point(-1, -1));
                Cv2.Erode(subtracted_mat, subtracted_mat, kernel, new OpenCvSharp.Point(-1, -1), 1);
                Cv2.GaussianBlur(subtracted_mat, subtracted_mat, new OpenCvSharp.Size(gaussianBlurLevel, gaussianBlurLevel), 0, 0);
                liveShow_image = subtracted_mat; //a copy of subtracted_mat
                Cv2.CvtColor(subtracted_mat, subtracted_mat, ColorConversionCodes.BGR2GRAY);
                Cv2.Threshold(subtracted_mat, subtracted_mat, threshold_value, 255, ThresholdTypes.BinaryInv);
                //霍夫圆变换找圆形区域(车头)
                circleSegments = Cv2.HoughCircles(subtracted_mat, HoughMethods.Gradient, 1, subtracted_mat.Rows / 8, 100, 10, 0, 40);
                for (int i = 0; i < circleSegments.Length; i++) Cv2.Circle(m, new OpenCvSharp.Point(circleSegments[i].Center.X, circleSegments[i].Center.Y), (int)circleSegments[i].Radius, color_yellow, 2);
                label5.Text = "NumOfHoughCircle: " + circleSegments.Length;
                /*
                 * 从帧差法结果图像中找一系列外接矩形的集合
                 */
                Cv2.Canny(subtracted_mat, subtracted_mat, minVal, maxVal);//边缘检测 return a binary image, which is used for cv2::findContours
                //寻找外接轮廓
                Cv2.FindContours(subtracted_mat, out contours, out hierarchly, RetrievalModes.External, ContourApproximationModes.ApproxNone);
                subtracted_mat = Mat.Zeros(subtracted_mat.Size(), m.Type()); //上色
                label8.Text = "NumOfMinAreaRect: " + contours.Length.ToString();
                //找到最大的 最小矩形(即定位到了小车位置) 并绘制
                Point2f[] point2F;
                for (int i = 0; i < contours.Length; i++)
                {
                    if (contours.Length > 0)
                    {
                        //drawContours  0表示只检测1个轮廓 -1表示检测所有轮廓
                        Cv2.DrawContours(liveShow_image, contours, 0, color_orange, 2, LineTypes.Link8, hierarchly);
                        pictureBox2.Image = liveShow_image.ToBitmap();//把轮廓信息绘制在pictureBox2上，以便对比、观察

                        Rect_only = Cv2.MinAreaRect(contours[0]);//该最小外接矩形，定位摄像头下的小车位置
                        //获取到小车四个顶点的坐标信息之后，绘制出来，color_orange 橙色矩形
                        point2F = Rect_only.Points();
                        Cv2.Line(m, (int)point2F[0].X, (int)point2F[0].Y, (int)point2F[1].X, (int)point2F[1].Y, color_orange, 2);
                        Cv2.Line(m, (int)point2F[1].X, (int)point2F[1].Y, (int)point2F[2].X, (int)point2F[2].Y, color_orange, 2);
                        Cv2.Line(m, (int)point2F[2].X, (int)point2F[2].Y, (int)point2F[3].X, (int)point2F[3].Y, color_orange, 2);
                        Cv2.Line(m, (int)point2F[3].X, (int)point2F[3].Y, (int)point2F[0].X, (int)point2F[0].Y, color_orange, 2);

                        if (circleSegments.Length > 0)
                        {
                            Cv2.Line(m, (int)circleSegments[0].Center.X, (int)circleSegments[0].Center.Y, (int)Rect_only.Center.X, (int)Rect_only.Center.Y, color_blue, 2);//连接霍夫圆的圆心和最小外接矩形的形心，蓝色线
                            Cv2.PutText(m, "Head:(" + (int)circleSegments[0].Center.X + "," + (int)circleSegments[0].Center.Y + ")", new OpenCvSharp.Point(circleSegments[0].Center.X, circleSegments[0].Center.Y), HersheyFonts.HersheyDuplex, 0.5, color_green);
                            Cv2.PutText(m, "Center:(" + (int)Rect_only.Center.X + "," + (int)Rect_only.Center.Y + ")", new OpenCvSharp.Point(Rect_only.Center.X, Rect_only.Center.Y), HersheyFonts.HersheyDuplex, 0.5, color_green);
                            if (radiobutton1checked) //自由模式
                            {
                                Cv2.PutText(m, "targetPoint:(" + targetPoint.X + "," + targetPoint.Y + ")", new OpenCvSharp.Point(targetPoint.X, targetPoint.Y), HersheyFonts.HersheyDuplex, 0.5, color_green);
                            }

                            #region 
                            //求两中心点的斜率和截距  y=kx+b
                            if (circleSegments[0].Center.X - Rect_only.Center.X != 0)
                            {
                                k = (double)(circleSegments[0].Center.Y - Rect_only.Center.Y) / (double)(circleSegments[0].Center.X - Rect_only.Center.X);
                                b = circleSegments[0].Center.Y - k * Rect_only.Center.X;
                                label9.Text = "两中心点连线直线方程: y=" + k.ToString() + "x+ " + b.ToString();
                            }

                            //判断目标点在直线上还是直线下
                            subtraction_result = targetPoint.Y - (k * targetPoint.X + b);
                            if (subtraction_result > 0) label13.Text = "目标点在直线" + "下方";
                            else label13.Text = "目标点在直线" + "上方";
                            #endregion

                            //将180°扩展为360°
                            double q = Math.Pow(point2F[2].Y - point2F[1].Y, 2) + Math.Pow(point2F[2].X - point2F[1].X, 2);
                            double w = Math.Pow(point2F[3].Y - point2F[2].Y, 2) + Math.Pow(point2F[3].X - point2F[2].X, 2);
                            double c = Math.Pow(circleSegments[0].Center.Y - point2F[0].Y, 2) + Math.Pow(circleSegments[0].Center.X - point2F[0].X, 2);
                            double v = Math.Pow(circleSegments[0].Center.Y - point2F[2].Y, 2) + Math.Pow(circleSegments[0].Center.X - point2F[2].X, 2);
                            double circelAngle = 9999.9999;
                            if (q > w) //1.1
                            {
                                double j1 = Cv2.FastAtan2(point2F[2].Y - point2F[1].Y, point2F[2].X - point2F[1].X);
                                double j2 = Cv2.FastAtan2(targetPoint.Y - Rect_only.Center.Y, targetPoint.X - Rect_only.Center.X);

                                if (c > v) //1.2
                                {
                                    circelAngle = j2 - j1;
                                    if (circelAngle > 180) circelAngle = circelAngle - 360;
                                    if (circelAngle < -180) circelAngle = circelAngle + 360;
                                }
                                else
                                {
                                    circelAngle = j1 - j2;
                                    circelAngle = 180 - circelAngle;
                                    if (circelAngle > 180) circelAngle = circelAngle - 360;
                                    if (circelAngle < -180) circelAngle = circelAngle + 360;
                                }
                                label40.Text = "偏移角度: " + ((int)circelAngle).ToString();
                            }
                            else if (q < w) //2.1
                            {
                                double j1 = Cv2.FastAtan2(point2F[3].Y - point2F[2].Y, point2F[3].X - point2F[2].X);
                                double j2 = Cv2.FastAtan2(targetPoint.Y - Rect_only.Center.Y, targetPoint.X - Rect_only.Center.X);
                                if (c > v) //2.2
                                {
                                    circelAngle = j1 - j2;
                                    circelAngle = 180 - circelAngle;
                                    if (circelAngle > 180) circelAngle = circelAngle - 360;
                                    if (circelAngle < -180) circelAngle = circelAngle + 360;
                                }
                                else
                                {
                                    circelAngle = j2 - j1;
                                    if (circelAngle > 180) circelAngle = circelAngle - 360;
                                    if (circelAngle < -180) circelAngle = circelAngle + 360;
                                }
                                label40.Text = "偏移角度: " + ((int)circelAngle).ToString();
                            }
                            double distance = Cv2.PointPolygonTest(contours[0], new Point2f(targetPoint.X, targetPoint.Y), false);

                            if (distance < 0)
                            {
                                label7.Text = "targetPoint尚不在轮廓内";
                                if (circelAngle != 9999.9999)
                                {
                                    if (circelAngle < -20 && circelAngle > -90) rightGO();
                                    if (circelAngle < -90 && circelAngle > -160) leftGO();
                                    if (circelAngle > 20 && circelAngle < 90) leftGO();
                                    if (circelAngle > 90 && circelAngle < 160) rightGO();
                                    if ((circelAngle < 20 && circelAngle > -20)) go_straight();
                                    if ((circelAngle < 180 && circelAngle > 160) || (circelAngle > -180 && circelAngle < -160)) back_straight();
                                }
                            }
                            else
                            {
                                label7.Text = "targetPoint已经在轮廓内";
                                all_stop();
                            }
                            if (j != 0) label15.Text = "命令数组a[" + j + "]= " + a[j - 1].ToString();
                        }
                    }
                }
                pictureBox4.BackgroundImage = m.ToBitmap();
            }
        }

        private void go_straight()
        {
            if (a[j - 1] != "GOstraight")
            {
                a[j] = "GOstraight";
                if (client != null && client.Connected)
                {
                    client.Send(Encoding.UTF8.GetBytes("g"));
                    j++;
                }
            }
        }

        private void back_straight()
        {
            if (a[j - 1] != "BACK")
            {
                a[j] = "BACK";
                if (client != null && client.Connected)
                {
                    client.Send(Encoding.UTF8.GetBytes("!"));
                    j++;
                }
            }
        }

        private void leftGO()
        {
            if (a[j - 1] != "leftGO")
            {
                a[j] = "leftGO";
                if (client != null && client.Connected)
                {
                    client.Send(Encoding.UTF8.GetBytes("a"));
                    j++;
                }
            }
        }

        private void rightGO()
        {
            if (a[j - 1] != "rightGO")
            {
                a[j] = "rightGO";
                if (client != null && client.Connected)
                {
                    client.Send(Encoding.UTF8.GetBytes("d"));
                    j++;
                }
            }
        }

        private void all_stop()
        {
            if (a[j - 1] != "停止")
            {
                a[j] = "停止";
                if (client != null && client.Connected)
                {
                    client.Send(Encoding.UTF8.GetBytes("."));
                    j++;
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress iPAddress = IPAddress.Parse("192.168.4.1");
            EndPoint endPoint = new IPEndPoint(iPAddress, 8080);
            client.Connect(endPoint);
            if (client.Connected)
            {
                button3.Text = "连接成功";
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (client != null && client.Connected)
                client.Send(Encoding.UTF8.GetBytes("."));
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            this.threshold_value = trackBar1.Value;
            label12.Text = this.threshold_value.ToString();
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            gaussianBlurLevel = (int)numericUpDown2.Value;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            erodeLevel = (int)numericUpDown1.Value;
        }

        private void pictureBox4_MouseClick(object sender, MouseEventArgs e)
        {
            if (radiobutton1checked)
            {
                targetPoint = e.Location;
                if (g != null)
                {
                    g.DrawEllipse(new Pen(Color.White, 5), targetPoint.X, targetPoint.Y, 1, 1);
                    if (contours.Length > 0 && circleSegments.Length > 0)
                    {
                        g.DrawLine(pen0, new System.Drawing.Point((int)Rect_only.Center.X, (int)Rect_only.Center.Y), targetPoint); //2个矩形的
                    }
                }
                pictureBox4.Image = bitmap;
            }
        }

        private void button_highspeed_Click(object sender, EventArgs e)
        {
            if (client != null && client.Connected)
                client.Send(Encoding.UTF8.GetBytes("1"));
        }

        private void button_mediumspeed_Click(object sender, EventArgs e)
        {
            if (client != null && client.Connected)
                client.Send(Encoding.UTF8.GetBytes("2"));
        }

        private void button_lowspeed_Click(object sender, EventArgs e)
        {
            if (client != null && client.Connected)
                client.Send(Encoding.UTF8.GetBytes("3"));
        }

        private void button2_Click(object sender, EventArgs e)//加载预设值
        {
            trackBar1.Value = 74;
            numericUpDown1.Value = 1;
            numericUpDown2.Value = 37;
            targetPoint = new System.Drawing.Point(pictureBox4.Width - 100, pictureBox4.Height - 100);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (client != null && client.Connected)
                client.Send(Encoding.UTF8.GetBytes("5"));
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (!timer2.Enabled)
            {
                timer2.Start();
            }
        }

        private int tir = 0;

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (ps > 0)
            {
                if (tir < ps)
                {
                    targetPoint = points_record[tir];
                    if (g != null)
                    {
                        g.DrawEllipse(pen0, targetPoint.X, targetPoint.Y, 4, 4);
                    }

                    pictureBox4.Image = bitmap;

                    tir = tir + 10;
                }
                else
                {
                    timer2.Enabled = false;
                }
            }
        }

        private bool radiobutton1checked = false;
        private bool radiobutton2checked = false;

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked == true)
            {
                radiobutton1checked = true;
            }
            else
            {
                radiobutton1checked = false;
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked == true)
            {
                radiobutton2checked = true;
            }
            else
            {
                radiobutton2checked = false;
            }
        }

        private Graphics g;
        private Bitmap bitmap;
        private bool is_pen_down = false; //画笔落下
        private bool is_first_point = false;
        private Pen pen0 = new Pen(Color.WhiteSmoke, 2);
        private System.Drawing.Point[] points_record = new System.Drawing.Point[65536];
        private int ps = 0;
        private System.Drawing.Point p1, p2;

        private void pictureBox4_MouseDown(object sender, MouseEventArgs e)
        {
            is_pen_down = true;
            is_first_point = true;
            if (g != null)
                g.Clear(Color.Empty);
            ps = 0;
            tir = 0;
        }

        private void pictureBox4_MouseMove(object sender, MouseEventArgs e)
        {
            if (radiobutton2checked)
            {
                if (is_first_point)
                {
                    p1 = new System.Drawing.Point(e.X, e.Y);
                    is_first_point = false;
                }
                if (is_pen_down)
                {
                    p2 = new System.Drawing.Point(e.X, e.Y);
                    if (g != null)
                    {
                        g.DrawLine(pen0, p1, p2);
                    }
                    p1 = p2;

                    points_record[ps] = e.Location;
                    ps++;
                    label6.Text = "本次轨迹长度: " + (ps - 1).ToString();
                }
                pictureBox4.Image = bitmap;
            }
        }

        private void pictureBox4_MouseUp(object sender, MouseEventArgs e)
        {
            is_pen_down = false;
            timer2.Stop();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            backgroundFrameMat = cap.RetrieveMat();
            //膨胀, 解决了地板砖侵蚀小车的问题
            Mat kernal0 = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(11, 11), new OpenCvSharp.Point(-1, -1));
            Cv2.Dilate(backgroundFrameMat, backgroundFrameMat, kernal0, new OpenCvSharp.Point(-1, -1), 1);
            pictureBox5.Image = backgroundFrameMat.ToBitmap();
            backgroundFrameMatIsCaptured = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (client.Connected)
            {
                client.Close();
                button3.Text = "连接下位机";
            }
        }
    }

}