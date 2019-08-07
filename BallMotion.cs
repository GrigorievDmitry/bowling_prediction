using System.Numerics;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VRABowling
{
    public class BallMotion
    {
        Socket socket, conn;
        bool connected = false;

        private Predictor pred;
        private float delay_mean;
        public Vector3 pos;

        Vector3 offset = Vector3.Zero;
        Vector3 scale = Vector3.One;

        int detNum;
        float gridStep, ballRadius, delay, shift;

        public void Start()
        {
            var conf = Parse();

            detNum = int.Parse(conf["detNum"]);

            gridStep = float.Parse(conf["gridStep"], CultureInfo.InvariantCulture);
            ballRadius = float.Parse(conf["ballRadius"], CultureInfo.InvariantCulture);
            delay = float.Parse(conf["delay"], CultureInfo.InvariantCulture);
            shift = float.Parse(conf["shift"], CultureInfo.InvariantCulture);
            delay_mean = delay;

            SetInputConnection();
        }

        void StartPredictor()
        {
            pred = new Predictor(detNum, gridStep, ballRadius, delay, shift);
            pred.Start();
        }

        void Update()
        {
            pos = pred.Predict(delay_mean);

            pos.X *= scale.X;
            pos.Y *= scale.Y;
            pos.Z *= scale.Z;
            pos += offset;
        }

        public void SetInputConnection()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint addr = new IPEndPoint(IPAddress.Any, 9292);
            socket.Bind(addr);
            socket.Listen(1);
            conn = socket.Accept();
            connected = true;

            StartPredictor();
            ProcessInputData();
        }

        void ProcessInputData()
        {
            try
            {
                byte[] row = new byte[64];
                while (connected)
                {
                    int bytesRecieved = conn.Receive(row);
                    if (bytesRecieved > 0)
                    {
                        var input = Encoding.Default.GetString(row);

                        if (input.StartsWith("GET")) { Respond(); }
                        if (input.StartsWith("SET")) { SetParameter(input); }
                        if (input.StartsWith("STOP"))
                        {
                            Disconnect();
                            conn = socket.Accept();
                            StartPredictor();
                        }
                        if (input.StartsWith("SHUTDOWN"))
                        {
                            connected = false;
                            Disconnect();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (SocketException se)
            {
                Console.WriteLine("SocketException : {0}", se.ToString());
            }
        }

        void Respond()
        {
            Update();
            conn.Send(Encoding.Default.GetBytes(pos.ToString() + "\n"));
        }

        void SetParameter(string input)
        {
            string directive = Regex.Match(input, @"\b[a-z]+=\(\d+\.?\d*,\d+\.?\d*,\d+\.?\d*\)").Value;
            string[] words = directive.Split('=');
            MatchCollection adjValues = Regex.Matches(words[1], @"\d+\.?\d*");
            Vector3 value = new Vector3(float.Parse(adjValues[0].Value),
                float.Parse(adjValues[1].Value), float.Parse(adjValues[2].Value));
            if (words[0] == "offset") { offset = value; }
            if (words[0] == "scale") { scale = value; }
        }

        void Disconnect()
        {
            conn.Shutdown(SocketShutdown.Both);
            conn.Close();
        }

        public void Stop()
        {
            pred.Disconnect();
            socket.Close();
        }

        Dictionary<string, string> Parse()
        {
            string path = System.IO.Directory.GetCurrentDirectory() + "\\bowling.conf";
            System.IO.FileInfo conf = new System.IO.FileInfo(path);
            if (conf.Exists)
            {
                Dictionary<string, string> output = new Dictionary<string, string>();
                using (System.IO.StreamReader sr = new System.IO.StreamReader(conf.FullName))
                {
                    String line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = Regex.Replace(line, @"\s+", "");
                        string[] words = line.Split('=');
                        output.Add(words[0], words[1]);
                    }
                }
                return output;
            }
            else
            {
                throw new Exception("squid_config is missing");
            }
        }
    }

class Program
    {
        static void Main()
        {
            BallMotion ballProcessor = new BallMotion();
            ballProcessor.Start();
            ballProcessor.Stop();
        }
    }
}