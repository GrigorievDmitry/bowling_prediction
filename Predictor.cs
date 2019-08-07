using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Numerics;


namespace VRABowling
{
    public class Predictor
    {
        Socket socket;
        IPEndPoint server;
        newSide[] sides;
        float shift, delay, ballRadius, gridStep;
        int detNum;

        bool Connected = false;
        public string IP = "192.168.137.2";
        public int Port = 9090;

        static readonly float k = (float)(1.0 / Math.Sqrt(2));
        static readonly byte[] sigStart = new byte[] { 1 };
        static readonly byte[] sigStop = new byte[] { 2 };

#if DEBUG
        System.IO.StreamWriter gcf, obtf;
#endif

        /// <summary>
        /// Class constructor.</summary>
        public Predictor(int detNum, float gridStep, float ballRadius, float delay, float shift)
        {
            this.shift = shift;
            this.delay = delay;
            this.detNum = detNum;
            this.ballRadius = ballRadius;
            this.gridStep = gridStep;
        }

        /// <summary>
        /// Begin tracking.</summary>
        public void Start()
        {
            if (!Connected)
                Connect();
            sides = new newSide[4]
            {
                new newSide(detNum, gridStep, ballRadius, 0), // left
                new newSide(detNum, gridStep, ballRadius, 1), // right
                new newSide(detNum, gridStep, ballRadius, 2), // second_left
                new newSide(detNum, gridStep, ballRadius, 3)  // second_right
            };
#if DEBUG
            InitLog();
#endif
            socket.Send(sigStart);
        }

        /// <summary>
        /// Stop tracking.</summary>
        public void Stop()
        {
            socket.Send(sigStop);
#if DEBUG
            CloseLog();
#endif
        }

        /// <summary>
        /// Get ball coordiantes.</summary>
        public Vector3 Predict(float delay, int lane)
        {
            float x = sides[1 + 2 * lane].GetCoord(delay);
            float y = sides[0 + 2 * lane].GetCoord(delay);
           
            return new Vector3((x - y) * k, 0.2f, (x + y) * k + shift);
            //return new Vector3((x - y) * k, 300 - (x + y) * k + shift, 0);
        }

        /// <summary>
        /// Connect to the server.</summary>
        public void Connect()
        {
            IPAddress ipAddress = Dns.GetHostAddresses(IP)[0];
            server = new IPEndPoint(ipAddress, Port);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(server);
#if DEBUG
            Console.WriteLine("Socket connected to {0}", socket.RemoteEndPoint.ToString());
#endif
            Connected = true;
            Thread socketThread = new Thread(new ThreadStart(ListenToServer));
            socketThread.Start();
        }

        /// <summary>
        /// Disconnect from the server and stop listener thread.</summary>
        public void Disconnect()
        {
            Stop();
            Connected = false;
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        /// <summary>
        /// Disconnect from the server and stop listener thread.</summary>
        public bool IsConnected()
        {
            return Connected;
        }

        /// <summary>
        /// Server listener thread worker.</summary>
        void ListenToServer()
        {
            try
            {
                byte[] row = new byte[12];
                while (true)
                {
                    int bytesRecieved = socket.Receive(row);
                    if (bytesRecieved > 0)
                    {
                        var side = BitConverter.ToUInt32(row, 0);
                        var detector = BitConverter.ToInt32(row, 4);
                        var time = BitConverter.ToSingle(row, 8);
                        if (detector < detNum)
                            sides[side].OnBallTracked(detector, time);
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

#if DEBUG
        /// <summary>
        ///Initiate logging.</summary>
        void InitLog()
        {
            String tm = DateTime.Now.ToString("MM-dd-yy H-mm-ss");
            gcf = new System.IO.StreamWriter(System.IO.Path.GetFullPath(tm + " GetCoord.log"));
            obtf = new System.IO.StreamWriter(System.IO.Path.GetFullPath(tm + " OnBallTracked.log"));
            
            System.IO.StreamWriter paramf = new System.IO.StreamWriter(System.IO.Path.GetFullPath(tm + " Parameters.log"));
            paramf.WriteLine("detNum = {0}", detNum);
            paramf.WriteLine("gridStep = {0}", gridStep);
            paramf.WriteLine("ballRadius = {0}", ballRadius);
            paramf.WriteLine("delay = {0}", delay);
            paramf.WriteLine("shift = {0}", shift);
            paramf.Close();

            obtf.WriteLine("{0,12}|{1,12}|{2,12}|{3,12}|{4,12}|{5,12}|{6,12}|{7,12}",
                "side", "ray", "time", "speed", "speed_corr", "coord[ray]-x0", "k", "s");
            gcf.WriteLine("{0,12}|{1,12}|{2,12}|{3,12}|{4,12}|{5,12}",
                "time", "prev_ray", "dt", "x", "speed+corr", "wait_mode");

            sides[0].gcf = gcf;
            sides[1].gcf = gcf;
            sides[0].obtf = obtf;
            sides[1].obtf = obtf;
        }

        /// <summary>
        /// Close log files.</summary>
        void CloseLog()
        {
            gcf.Close();
            obtf.Close();
        }
#endif
    }
}
