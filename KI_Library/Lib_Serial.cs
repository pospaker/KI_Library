using System;
using System.IO.Ports;
using System.Text;

namespace KINT_Lib
{
    public class Lib_SerialPort
    {
        private SerialPort _serialPort;

        // 델리게이트 정의
        public delegate void DataReceivedHandler(string data);

        // 이벤트 선언
        public event DataReceivedHandler OnDataReceived;

        public Lib_SerialPort(string portName, int baudRate = 9600)
        {
            _serialPort = new SerialPort(portName, baudRate);
            _serialPort.Encoding = Encoding.UTF8;
            _serialPort.DataReceived += SerialPort_DataReceived;
        }

        // 포트 열기
        public void Open()
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
            }
        }

        // 포트 닫기
        public void Close()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        // 데이터 수신 이벤트 처리
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadExisting();

                // 델리게이트 호출 (다른 스레드일 수 있으므로 주의)
                OnDataReceived?.Invoke(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.Message}");
            }
        }

        // byte[] 데이터 전송
        public void Send(byte[] data)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Write(data, 0, data.Length);
            }
            else
            {
                Console.WriteLine("포트가 열려 있지 않습니다.");
            }
        }

        // string 데이터 전송
        public void Send(string data)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Write(data);
            }
            else
            {
                Console.WriteLine("포트가 열려 있지 않습니다.");
            }
        }
    }
}