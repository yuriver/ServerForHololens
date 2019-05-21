using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

public class Server : MonoBehaviour
{
    
    private const int PORT = 6067;
    private Thread listeningThread = null;
    private TcpListener server = null;
    private bool serverRunning = true;

    private byte[] testBuffer = null;

    private Texture2D testTexture;
    public UnityEngine.UI.Image testImageView;

    private Stream stream = null;

    void Start()
    {
        Application.runInBackground = true;
        Listening();
    }


    void Update()
    {
        if (testBuffer != null)
        {
            Texture2D tex = ImageUtil.RawToTexture2D(testBuffer);
            Sprite sprite = ImageUtil.TextureToSprite(tex);

            testImageView.sprite = sprite;
            testBuffer = null;
        }
    }

    void OnApplicationQuit()
    {
        StopServer();      
    }

    void StopServer()
    {
        if (listeningThread.IsAlive)
        {
            listeningThread.Abort();
        }

        serverRunning = false;


        try
        {
            server.Stop();
        }
        catch(Exception e)
        {
            UnityEngine.Debug.Log(e.Message);
        }
    }

    private void Listening()
    {
        server = new TcpListener(IPAddress.Any, PORT);
        server.Start();
        UnityEngine.Debug.Log("Ready...");
        
        listeningThread = new Thread(() =>
        {
            while (serverRunning)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    stream = client.GetStream();
                    UnityEngine.Debug.Log("Connected client.");

                    Thread receivingThread = new Thread(Receiving);
                    receivingThread.Start();
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

    private void Receiving()
    {
        byte[] imageRawData = null;
        Vector3 hmdPosition, hmdRotation;
        
        while (serverRunning)
        {
            try
            {
                /* 
                 * first message header
                 * [ size of second message(int) ]
                
                 * second message body
                 * [ hmdPosition.x | hmdPosition.y | hmdPosition.z : hdmRotation.x | hmdRotation.y | hmdRotation.z : rawdataLength(int) : image raw data ]
                 */


                // read message header
                int n = 0;
                if ((n = ReadMessageHeader()) > 0)
                {
                    byte[] data = ReadMessageBody(n);

                    ParseData(data.ToList(), out hmdPosition, out hmdRotation, out imageRawData);

                    // light inference and send message
                    Processing(hmdPosition, hmdRotation,imageRawData);
                } 
                else
                {
                    break;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.Message + "\n" + e.StackTrace);
                stream = null;
                break;
            }
        }

        if(stream != null)
        {
            stream.Close();
            stream.Dispose();
            stream = null;
        }
    }

    private int ReadMessageHeader()
    {
        int bodyLength = -1;
        byte[] buffer = null;

        try
        {
            buffer = new byte[sizeof(int)];
            stream.Read(buffer, 0, buffer.Length);
            bodyLength = BitConverter.ToInt32(buffer, 0);            

            UnityEngine.Debug.Log("message header received: " + bodyLength);
        }
        catch(Exception e)
        {
            UnityEngine.Debug.Log(e.Message + "\n" + e.StackTrace);
        }

        return bodyLength;
    }

    private byte[] ReadMessageBody(int messageLength)
    {
        int remained = messageLength;
        byte[] buffer = null;
        List<byte> message = new List<byte>();

        while(remained > 0)
        {
            buffer = new byte[remained > 1024 ? 1024 : remained];
            stream.Read(buffer, 0, buffer.Length);

            message.AddRange(buffer);
            remained -= buffer.Length;
            buffer.Initialize();            
        }
                
        return message.ToArray();
    }

    private void ParseData(List<byte> data, out Vector3 hmdPosition, out Vector3 hmdRotation, out byte[] imageRawdata)
    {
        try
        {
            string temp = Encoding.UTF8.GetString(data.ToArray());

            int firstColon = temp.IndexOf(':', 0);
            int secondColon = temp.IndexOf(':', firstColon + 1);

            string[] position = temp.Substring(0, firstColon).Split('|');
            string[] rotation = temp.Substring(firstColon + 1, secondColon - firstColon - 1).Split('|');
            int imageOffset = secondColon + 1;

            //UnityEngine.Debug.LogFormat("HmdInfo Length: {0}", secondColon + 1);
            //UnityEngine.Debug.LogFormat("{0} : {1}", string.Join(", ", position), string.Join(", ", rotation));

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

            imageRawdata = data.GetRange(imageOffset, data.Count - imageOffset).ToArray();
            //imageRawdata = Encoding.UTF8.GetBytes(data);
        }        
        catch(Exception e)
        {
            UnityEngine.Debug.Log("data: " + data + "\n" + e.Source + "\n" + e.StackTrace);

            hmdPosition = hmdRotation = Vector3.zero;
            imageRawdata = null;
        }        
    }

    private void SendTCPMessage(string data)
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
        else
        {
            UnityEngine.Debug.Log("Stream is null!");
        }
    }

    private async Task Processing(Vector3 hmdPosition, Vector3 hmdRotation, byte[] imageRawData)
    {
        string imagePath = await SaveImage(imageRawData);
        await Cropping(imagePath);

        testBuffer = File.ReadAllBytes(imagePath);

        string xmlPath = await Inference(imagePath);
        DetectionBox[] boxes = await ParseToXml(xmlPath);

        Message message = new Message()
        {
            hmdPosition = hmdPosition,
            hmdRotation = hmdRotation,
            boxes = boxes
        };

        if(message.boxes.Length > 0)
        {
            SendTCPMessage(message.ToString());
        }
        else
        {
            UnityEngine.Debug.Log("Not detected.");
        }
        
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

    private readonly string CROPPER_PATH = @"C:\HololensImages\image_cropper.py";
    private async Task Cropping(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            UnityEngine.Debug.LogFormat("doesn't exist image({0})", imagePath);
            return;
        }


        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = "python",
            Arguments = string.Format("{0} {1}", CROPPER_PATH, imagePath),
            UseShellExecute = false,
            RedirectStandardOutput = true
        };

        using (Process process = Process.Start(startInfo))
        {
            using (StreamReader reader = process.StandardOutput)
            {
                await reader.ReadToEndAsync();
            }
        }
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
            Arguments = string.Format("{0}", imagePath),
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
