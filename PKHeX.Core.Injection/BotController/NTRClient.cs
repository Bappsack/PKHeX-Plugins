﻿using System.Threading;

namespace PKHeX.Core.Injection
{
    public class NTRClient : ICommunicator
    {
        private string IP = "192.168.1.106";
        private int Port = 8000;
        private static readonly NTR clientNTR = new NTR();

        private const int timeout = 10;
        private bool Connected;

        private readonly object _sync = new object();
        private byte[]? _lastMemoryRead;

        public void Connect()
        {
            clientNTR.Connect(IP, Port);
            if (clientNTR.IsConnected)
                Connected = true;
        }

        bool ICommunicator.Connected { get => Connected; set => Connected = value; }
        int ICommunicator.Port { get => Port; set => Port = value; }
        string ICommunicator.IP { get => IP; set => IP = value; }

        public void Disconnect()
        {
            lock (_sync)
            {
                clientNTR.Disconnect();
                Connected = false;
            }
        }

        private void HandleMemoryRead(object argsObj)
        {
            DataReadyWaiting args = (DataReadyWaiting)argsObj;
            _lastMemoryRead = args.Data;
        }

        public byte[] ReadBytes(uint offset, int length)
        {
            lock (_sync)
            {
                if (!Connected) Connect();

                WriteLastLog("");
                DataReadyWaiting myArgs = new DataReadyWaiting(new byte[length], HandleMemoryRead, null);
                while (clientNTR.PID == -1)
                {
                    Thread.Sleep(10);
                }
                clientNTR.AddWaitingForData(clientNTR.Data(offset, (uint)length, clientNTR.PID), myArgs);

                for (int readcount = 0; readcount < timeout * 100; readcount++)
                {
                    Thread.Sleep(10);
                    if (CompareLastLog("finished"))
                        break;
                }

                byte[] result = _lastMemoryRead ?? System.Array.Empty<byte>();
                _lastMemoryRead = null;
                return result;
            }
        }

        private static void WriteLastLog(string str) => clientNTR.Lastlog = str;
        private static bool CompareLastLog(string str) => clientNTR.Lastlog.Contains(str);

        public void WriteBytes(byte[] data, uint offset)
        {
            lock (_sync)
            {
                if (!Connected) Connect();
                while (clientNTR.PID == -1)
                {
                    Thread.Sleep(10);
                }
                clientNTR.Write(offset, data, clientNTR.PID);
                int waittimeout;
                for (waittimeout = 0; waittimeout < timeout * 100; waittimeout++)
                {
                    WriteLastLog("");
                    Thread.Sleep(10);
                    if (CompareLastLog("finished"))
                        break;
                }
            }
        }
    }
}