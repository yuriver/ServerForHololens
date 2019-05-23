using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Xml;
using System.Text;

using UnityEngine;

public class XMLParser
{
    private readonly string ID          = "id";
    private readonly string SCORE       = "score";
    private readonly string BOX         = "box";
    private readonly string COLOR       = "color";
    private readonly string INTENSITY   = "intensity";
    private readonly string X_MIN       = "xmin";
    private readonly string Y_MIN       = "ymin";
    private readonly string X_MAX       = "xmax";
    private readonly string Y_MAX       = "ymax";
    private readonly string B           = "b";
    private readonly string G           = "g";
    private readonly string R           = "r";

    private readonly string SELECT_NODE_NAME = "/data/object";
    private string xmlPath = null;

    public XMLParser(string xmlPath)
    {
        this.xmlPath = xmlPath;
    }

    public DetectionBox[] ParseToBoxes()
    {
        if(xmlPath == null)
        {
            return null;
        }


        XmlDocument xmlDocument = new XmlDocument();
        xmlDocument.Load(xmlPath);

        XmlNodeList xmlList = xmlDocument.SelectNodes(SELECT_NODE_NAME);
        string[] parseData = new string[xmlList.Count];

        DetectionBox[] boxes = new DetectionBox[xmlList.Count];
        XmlNode node = null;
        for (int i = 0; i < xmlList.Count; i++)
        {
            node = xmlList[i];

            boxes[i] = new DetectionBox()
            {
                id = int.Parse(node[ID].InnerText),

                score = float.Parse(node[SCORE].InnerText),

                min = new Vector2()
                {
                    x = float.Parse(node[BOX][X_MIN].InnerText),
                    y = float.Parse(node[BOX][Y_MIN].InnerText)
                },

                max = new Vector2()
                {
                    x = float.Parse(node[BOX][X_MAX].InnerText),
                    y = float.Parse(node[BOX][Y_MAX].InnerText)
                },

                color = new Color()
                {
                    r = float.Parse(node[COLOR][R].InnerText),
                    g = float.Parse(node[COLOR][G].InnerText),
                    b = float.Parse(node[COLOR][B].InnerText),
                },

                intensity = float.Parse(node[INTENSITY].InnerText)
            };
        }

        return boxes;
    }
}
