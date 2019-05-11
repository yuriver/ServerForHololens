using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

using UnityEngine;

public class Server : MonoBehaviour
{
    
    private const int PORT = 6067;
    private Thread listeningThread = null;
    private bool serverRunning = true;

    public Texture2D testTexture;

    void Start()
    {
        Application.runInBackground = true;
        Listening();
    }


    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 pos = Input.mousePosition;
            UnityEngine.Debug.Log(pos.x + ", " + pos.y + ", " + pos.z);
        }
    }

    void OnApplicationQuit()
    {
        if (listeningThread.IsAlive)
        {
            listeningThread.Abort();
        }

        serverRunning = false;        
    }

    private void Listening()
    {
        TcpListener server = new TcpListener(IPAddress.Any, PORT);
        server.Start();
        UnityEngine.Debug.Log("Ready...");
        
        listeningThread = new Thread(() =>
        {
            while (serverRunning)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    UnityEngine.Debug.Log("Connected client.");

                    Thread receivingThread = new Thread(new ParameterizedThreadStart(Receiving));
                    receivingThread.Start(client as object);
                }
                catch(Exception e)
                {
                    UnityEngine.Debug.Log(e.Message);
                    serverRunning = false;
                    server.Stop();
                    break;
                }

            }

        });

        listeningThread.Start();
    }

    private void Receiving(object o)
    {
        TcpClient client = o as TcpClient;
        NetworkStream stream = client.GetStream();

        byte[] buffer = null;
        Vector3 hmdPosition, hmdRotation;

        int imageLength = 0;
        
        while (serverRunning)
        {
            try
            {
                /* 
                 * first message format
                 * [ size of second message(int) ]
                
                 * second message format
                 * [ hmdPosition.x | hmdPosition.y | hmdPosition.z : hdmRotation.x | hmdRotation.y | hmdRotation.z : rawdataLength(int) ]
                 
                 * third message format
                 * [ rawdata(bytes) ]
                 */

                // read messages.
                int n = 0;
                if ((n = ReadToFirstMessage(ref stream, ref buffer)) == 0)
                {
                    break;
                }
                
                imageLength = ReadToSecondMessage(ref stream, ref buffer, out hmdPosition, out hmdRotation);


                n = ReadToThirdMessage(ref stream, ref buffer);


                UnityEngine.Debug.LogFormat("second message's length: {0}\nthird message's length: {1}", imageLength, n);

                // light inference and send message
                Processing(stream, buffer, hmdPosition, hmdRotation);                
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.Message + "\n" + e.StackTrace);
                break;
            }
        }

        if(stream != null)
        {
            stream.Close();
            stream.Dispose();
            stream = null;
        }

        if(client != null)
        {
            client.Close();
            client.Dispose();
        }
    }

    private int ReadToFirstMessage(ref NetworkStream stream, ref byte[] buffer)
    {
        buffer = new byte[sizeof(int)];
        int nRead = stream.Read(buffer, 0, buffer.Length);
        
        int bufferSize = BitConverter.ToInt32(buffer, 0);
        buffer = new byte[bufferSize];

        UnityEngine.Debug.Log("first message received, nRead: " + nRead);

        return nRead;
    }

    private int ReadToSecondMessage(ref NetworkStream stream, ref byte[] buffer, out Vector3 hmdPosition, out Vector3 hmdRotation)
    {
        int nRead = stream.Read(buffer, 0, buffer.Length);
        string data = Encoding.UTF8.GetString(buffer);

        // get hmdPosition, hmdRotation, imageLength
        int imageLength = ParseData(data, out hmdPosition, out hmdRotation);
        buffer = new byte[imageLength];

        UnityEngine.Debug.Log("Received message 1/2.");

        return imageLength;
    }

    private int ReadToThirdMessage(ref NetworkStream stream, ref byte[] buffer)
    {
        //int nRead = stream.Read(buffer, 0, buffer.Length);

        int nRead = 0;
        byte[] temp = null;
        List<byte> array = new List<byte>();

        int remained = buffer.Length;
        UnityEngine.Debug.Log("remained: " + remained);

        while(remained > 0)
        {
            if (remained > 1024)
            {
                temp = new byte[1024];                
            }
            else
            {
                temp = new byte[remained];
            }

            stream.Read(temp, 0, temp.Length);
            remained -= temp.Length;
            array.AddRange(temp);            
        }

        buffer = array.ToArray();
        UnityEngine.Debug.Log("buffer: " + buffer.Length);

        UnityEngine.Debug.Log("Received message 2/2.");

        return buffer.Length;
    }

    private int ParseData(string data, out Vector3 hmdPosition, out Vector3 hmdRotation)
    {
        string[] parts = data.Split(':');

        string[] position = parts[0].Split('|');
        string[] rotation = parts[1].Split('|');
        string imageLength = parts[2];

        hmdPosition = new Vector3()
        {
            x = float.Parse(position[0]),
            y = float.Parse(position[1]),
            z = float.Parse(position[2])
        };

        hmdRotation = new Vector3()
        {
            x = float.Parse(rotation[0]),
            y = float.Parse(rotation[1]),
            z = float.Parse(rotation[2])
        };
        
        return int.Parse(imageLength);
    }

    private void SendTCPMessage(NetworkStream stream, string data)
    {
        if (stream != null)
        {
            Thread sendingThread = new Thread(() =>
            {
                byte[] buffer = Encoding.UTF8.GetBytes(data);
                byte[] bufferSize = BitConverter.GetBytes(buffer.Length);

                stream.Write(bufferSize, 0, bufferSize.Length);
                stream.Flush();

                stream.Write(buffer, 0, buffer.Length);
                stream.Flush();
            });

            sendingThread.Start();
        }
    }

    private async Task Processing(NetworkStream stream, byte[] imageRawData, Vector3 hmdPosition, Vector3 hmdRotation)
    {
        string imagePath = await SaveImage(imageRawData);
        string xmlPath = await Inference(imagePath);
        DetectionBox[] boxes = await ParseToXml(xmlPath);

        Message message = new Message()
        {
            hmdPosition = hmdPosition,
            hmdRotation = hmdRotation,
            boxes = boxes
        };

        SendTCPMessage(stream, message.ToString());
    }

    private Task<string> SaveImage(byte[] imageRawData)
    {
        string path = null;

        try
        {
            path = ImageUtil.SaveImage(imageRawData);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.Message);
            return null;
        }

        return Task.FromResult(path);
    }

    private readonly string BAT_FILE_PATH = @"C:\HololensImages\inference.bat";
    private async Task<string> Inference(string imagePath)
    {
        if(!File.Exists(imagePath))
        {
            UnityEngine.Debug.LogFormat("doesn't exist image({0})", imagePath);
            return null;
        }


        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = BAT_FILE_PATH,
            Arguments = imagePath,
            UseShellExecute = false,
            RedirectStandardOutput = true
        };
        
        using (Process process = Process.Start(startInfo))
        {
            using (StreamReader reader = process.StandardOutput)
            {
                string result = await reader.ReadToEndAsync();
                UnityEngine.Debug.LogFormat("xml path: {0}", result);

                return result;
            }
        }
    }

    private Task<DetectionBox[]> ParseToXml(string xmlPath)
    {
        XMLParser xmlParser = new XMLParser(xmlPath);
        DetectionBox[] boxes = xmlParser.ParseToBoxes();

        return Task.FromResult(boxes);
    }
}
