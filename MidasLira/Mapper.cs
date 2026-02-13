using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace MidasLira
{
    public static class Mapper  //GetNodesForElement - ïîñìîòðåòü îá èñïîëüçîâàíèè ëîããåðà èëè ñîîáùåíèÿ îá îøèáêå íå ÷åðåç êîíñîëü
    {
        private const double COORDINATE_EPSILON = 0.001; // Ïîãðåøíîñòü äëÿ ñðàâíåíèÿ êîîðäèíàò

        /// <summary>
        /// Ñòðîèò ñîîòâåòñòâèå ýëåìåíòîâ MIDAS è ËÈÐÀ-ÑÀÏÐ ïî îáùèì óçëàì.
        /// </summary>

        public static void MapNodesAndElements(
            List<MidasNodeInfo> midasNodes,
            List<LiraNodeInfo> liraNodes,
            List<MidasElementInfo> midasElements,
            List<LiraElementInfo> liraElements)
        {
            // 1. Ñîïîñòàâëåíèå óçëîâ ïî êîîðäèíàòàì 
            foreach (var midasNode in midasNodes)
            {
                var matchingLiraNode = liraNodes.FirstOrDefault(l =>
                    Math.Abs(l.X - midasNode.X) < COORDINATE_EPSILON &&
                    Math.Abs(l.Y - midasNode.Y) < COORDINATE_EPSILON &&
                    Math.Abs(l.Z - midasNode.Z) < COORDINATE_EPSILON);

                if (!matchingLiraNode.IsEmpty)
                {
                    midasNode.AppropriateLiraNode = matchingLiraNode;
                }
            }

            // 2. ÑÎÇÄÀ¨Ì ÑËÎÂÀÐÜ ÓÇËÎÂ MIDAS ÄËß ÁÛÑÒÐÎÃÎ ÄÎÑÒÓÏÀ
            var midasNodeDict = midasNodes.ToDictionary(n => n.Id);

            // 3. Ñîïîñòàâëåíèå ýëåìåíòîâ
            foreach (var midasElement in midasElements)
            {
                // Ñîáèðàåì ID óçëîâ ËÈÐÀ, ñîîòâåòñòâóþùèõ óçëàì MIDAS äàííîãî ýëåìåíòà
                var matchedNodeIds = midasElement.NodeIds
                    .Select(nodeId =>
                    {
                        // Ïîèñê ïî ñëîâàðþ – O(1)
                        if (midasNodeDict.TryGetValue(nodeId, out var midasNode) &&
                            !midasNode.AppropriateLiraNode.IsEmpty) // ÈÑÏÐÀÂËÅÍÎ ÓÑËÎÂÈÅ
                        {
                            return midasNode.AppropriateLiraNode.Id;
                        }
                        return 0;
                    })
                    .Where(id => id != 0)
                    .OrderBy(id => id)
                    .ToList();

                // Èùåì ýëåìåíò ËÈÐÀ ñ òî÷íî òàêèì æå íàáîðîì óçëîâ
                var matchingLiraElement = liraElements.FirstOrDefault(le =>
                    le.NodeIds.OrderBy(id => id).SequenceEqual(matchedNodeIds));

                if (!matchingLiraElement.IsEmpty)
                {
                    midasElement.AppropriateLiraElement = matchingLiraElement;
                }
            }
        }


        /// <summary>
        /// Ìåòîä êëàñòåðèçàöèè ýëåìåíòîâ ïî ïëèòàì ñ èñïîëüçîâàíèåì ñëîâàðÿ óçëîâ
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static List<Plaque> ClusterizeElements(List<MidasElementInfo> elements, Dictionary<int, MidasNodeInfo> nodeDictionary)
        {
            var plaques = new List<Plaque>();

            // Íàáîð ýëåìåíòîâ, êîòîðûå óæå ó÷òåíû â ïëèòàõ
            var processedElements = new HashSet<int>();

            foreach (var element in elements)
            {
                if (processedElements.Contains(element.Id))
                    continue; // Ýëåìåíò óæå âêëþ÷åí â êëàñòåð

                // Ñîçäàåì íîâóþ ïëèòó
                var plaque = new Plaque();
                plaque.Elements.Add(element);
                plaque.Nodes.AddRange(GetNodesForElement(element, nodeDictionary));

                var queue = new Queue<MidasElementInfo>();
                queue.Enqueue(element);
                processedElements.Add(element.Id);

                // DFS-îáõîä ñìåæíûõ ýëåìåíòîâ
                while (queue.Count > 0) //ãðàôîâûé ïîèñê, ãäå ýëåìåíòû — âåðøèíû ãðàôà, à îáùèå óçëû — ð¸áðà.
                {
                    var currentElement = queue.Dequeue();

                    // Ïðîñìàòðèâàåì âñå ýëåìåíòû
                    foreach (var otherElement in elements)
                    {
                        if (processedElements.Contains(otherElement.Id))
                            continue; // Ýëåìåíò óæå ó÷òåí

                        // Åñëè ýëåìåíòû èìåþò õîòÿ áû îäèí îáùèé óçåë
                        if (otherElement.NodeIds.Intersect(currentElement.NodeIds).Any())
                        {
                            plaque.Elements.Add(otherElement);
                            plaque.Nodes.AddRange(GetNodesForElement(otherElement, nodeDictionary));
                            queue.Enqueue(otherElement);
                            processedElements.Add(otherElement.Id);
                        }
                    }
                }
                plaque.Nodes = [.. plaque.Nodes.Distinct()];  // ñîêðàùåíèå plaque.Nodes = plaque.Nodes.Distinct().ToList();

                // Ïðèñâàèâàåì ïëèòå óíèêàëüíûé íîìåð
                plaque.Id = plaques.Count + 1;

                // Çàïîëíÿåì ïîëå PlankId â ýëåìåíòàõ è óçëàõ
                for (int i = 0; i < plaque.Elements.Count; i++)
                {
                    plaque.Elements[i].Plaque = plaque;
                }
                for (int j = 0; j < plaque.Nodes.Count; j++)
                {
                    plaque.Nodes[j].Plaque = plaque;
                }

                plaques.Add(plaque);
            }

            return plaques;
        }

        // Âñïîìîãàòåëüíûé ìåòîä äëÿ ïîëó÷åíèÿ óçëîâ ýëåìåíòà
        private static List<MidasNodeInfo> GetNodesForElement(MidasElementInfo element, Dictionary<int, MidasNodeInfo> nodeDictionary)
        {
            var foundNodes = new List<MidasNodeInfo>(element.NodeIds.Length);

            foreach (var nodeId in element.NodeIds)
            {
                if (nodeDictionary.TryGetValue(nodeId, out var node))
                {
                    foundNodes.Add(node);
                }
                else
                {
                    AppLogger.Warning($"Óçåë ñ ID={nodeId} íå íàéäåí äëÿ ýëåìåíòà ID={element.Id}");
                    // îòëàäî÷íàÿ èíôîðìàöèÿ
                    Console.WriteLine($"Ïðåäóïðåæäåíèå: Óçåë ñ ID={nodeId} íå íàéäåí äëÿ ýëåìåíòà ID={element.Id}");
                }
            }

            return foundNodes;
        }
        //Êàê ðàáîòàåò ìåòîä:
        //Initialization:  
        //Ñîçäàþòñÿ äâà ãëàâíûõ îáúåêòà:
        //plaques: ñïèñîê, â êîòîðûé áóäóò ñîáèðàòüñÿ ãîòîâûå ïëèòû.
        //processedElements: õåøñåò, â êîòîðîì õðàíÿòñÿ èäåíòèôèêàòîðû ýëåìåíòîâ, óæå âîøåäøèõ â êàêóþ-ëèáî ïëèòó.
        //Outer Loop:Ìåòîä ïðîõîäèò ïî êàæäîìó ýëåìåíòó èç ñïèñêà elements. Åñëè ýëåìåíò óæå âêëþ÷¸í â ïëèòó (ïðîâåðÿåòñÿ ïî õåøñåòó processedElements), îí ïðîïóñêàåòñÿ.
        //Creating a plaques:  
        //Äëÿ êàæäîãî íîâîãî ýëåìåíòà ñîçäà¸òñÿ íîâàÿ ïëèòà (ñïèñîê ýëåìåíòîâ), è ýòîò ýëåìåíò äîáàâëÿåòñÿ â î÷åðåäü (queue).
        //Çàòåì ýëåìåíò äîáàâëÿåòñÿ â õåøñåò processedElements, ÷òîáû îòìåòèòü, ÷òî îí óæå ó÷òåí.
        //DFS Traversal:  
        //Èñïîëüçóÿ î÷åðåäü (queue), ìû ïðîõîäèì ïî âñåì ñâÿçàííûì ýëåìåíòàì. Ýòî ñâîåãî ðîäà ãðàôîâûé ïîèñê, ãäå ýëåìåíòû — âåðøèíû ãðàôà, à îáùèå óçëû — ð¸áðà.
        //Ïîêà î÷åðåäü íå ïóñòà, ìû äîñòà¸ì ýëåìåíò èç î÷åðåäè è ïðîâåðÿåì âñå îñòàëüíûå ýëåìåíòû â ñïèñêå.
        //Åñëè íàéäåí ýëåìåíò, èìåþùèé õîòÿ áû îäèí îáùèé óçåë ñ òåêóùèì ýëåìåíòîì, îí äîáàâëÿåòñÿ â òåêóùóþ ïëèòó, â î÷åðåäü è îòìå÷àåòñÿ êàê îáðàáîòàííûé.
        //Adding Clusters:  
        //Ïîñëå òîãî êàê íàéäåíû âñå ñâÿçàííûå ýëåìåíòû, òåêóùàÿ ïëèòà äîáàâëÿåòñÿ â ñïèñîê plaques.
        // Repeat:Ïðîöåäóðà ïîâòîðÿåòñÿ äëÿ îñòàâøèõñÿ íåîáðàáîòàííûõ ýëåìåíòîâ.


        // Âñïîìîãàòåëüíûå ñòðóêòóðû
        public class MidasNodeInfo
        {
            public int Id { get; }
            public double X { get; }
            public double Y { get; }
            public double Z { get; }
            public double NodeDisplacement { get; }
            public List<MidasElementInfo> Elements { get; }
            public LiraNodeInfo AppropriateLiraNode { get; set; } = LiraNodeInfo.Empty; // ñîîòâåòñòâóþùèé óçåë â ËÈÐÀ-ÑÀÏÐ, èñïîëüçóåì Empty
            public Plaque Plaque { get; set; } // Íîìåð ïëèòû, ê êîòîðîé ïðèíàäëåæèò óçåë
            public int RigidityNumber { get; set; } // Íîìåð æåñòêîñòè äëÿ çàïèñè â ôàéë

            public MidasNodeInfo(int id, double x, double y, double z, double nodeDisplacement, List<MidasElementInfo>? elements = null)
            {
                Id = id;
                X = x;
                Y = y;
                Z = z;
                NodeDisplacement = nodeDisplacement;
                Elements = elements ?? []; // Èñïîëüçóåì íîâûé ñèíòàêñèñ
                AppropriateLiraNode = LiraNodeInfo.Empty; // èçíà÷àëüíî íå çíàåì ñîîòâåòñòâóþùèé óçåë â ËÈÐÀ-ÑÀÏÐ
                Plaque = new Plaque();
                RigidityNumber = 0; // Èçíà÷àëüíî íå çàäàí
            }
        }

        public class MidasElementInfo
        {
            public int Id { get; }
            public int[] NodeIds { get; } // Óçëû, ïðèíàäëåæàùèå ýëåìåíòó
            public double Stress { get; set; } // Íàïðÿæåíèå â ýëåìåíòå
            public double Displacement { get; } // Ïåðåìåùåíèå ýëåìåíòà
            public double BeddingCoefficient { get; set; } // Êîýôôèöèåíò ïîñòåëè (C1)
            public int PlankId { get; set; } // Íîìåð ïëèòû, ê êîòîðîé ïðèíàäëåæèò ýëåìåíò
            public LiraElementInfo AppropriateLiraElement { get; set; }
            public Plaque Plaque { get; set; }

            public MidasElementInfo(int id, int[] nodeIds, double stress, double displacement, double beddingCoefficient)
            {
                Id = id;
                NodeIds = nodeIds;
                Stress = stress;
                Displacement = displacement;
                BeddingCoefficient = beddingCoefficient;
                PlankId = -1; // Èçíà÷àëüíî ýëåìåíò íå ïðèêðåïë¸í íè ê êàêîé ïëèòå
                AppropriateLiraElement = new LiraElementInfo(); // èçíà÷àëüíî íå çíàåì ñîîòâåòñòâóþùèé ýëåìåíò â ËÈÐÀ-ÑÀÏÐ
                Plaque = new Plaque();
            }
        }

        public readonly struct LiraNodeInfo: IEquatable<LiraNodeInfo>
        {
            private const double Epsilon = 1e-6;  // Äîïóñê 0.001 ìì
            public int Id { get; }
            public double X { get; }
            public double Y { get; }
            public double Z { get; }
            public List<LiraElementInfo> Elements { get; }

            public LiraNodeInfo(int id, double x, double y, double z, List<LiraElementInfo> elements)
            {
                Id = id;
                X = x;
                Y = y;
                Z = z;
                Elements = elements ?? [];
            }

            // Còàòè÷åñêîå ñâîéñòâî äëÿ "ïóñòîãî" çíà÷åíèÿ
            public static LiraNodeInfo Empty => new(0, 0, 0, 0, []);

            // Ìåòîä äëÿ ïðîâåðêè íà "ïóñòîòó"
            public bool IsEmpty => Id == 0;

            // Ðåàëèçàöèÿ IEquatable äëÿ èçáåæàíèÿ áîêñèíãà
            public bool Equals (LiraNodeInfo other)
            {
                return Id == other.Id && 
                    Math.Abs(X - other.X) < Epsilon &&
                    Math.Abs (Y - other.Y) < Epsilon &&
                    Math.Abs (Z- other.Z) < Epsilon;
            }

            // Ïåðåîïðåäåëÿåì Equals äëÿ êîððåêòíîãî ñðàâíåíèÿ
            public override bool Equals(object? obj)
            {
                return obj is LiraNodeInfo other && Equals(other);
            }

            // GetHashCode äîëæåí áûòü ñîãëàñîâàí ñ Equals
            public override int GetHashCode()
            {
                // Îêðóãëÿåì êîîðäèíàòû äëÿ õýø-êîäà
                int xHash = (int)(X / Epsilon);
                int yHash = (int)(Y / Epsilon);
                int zHash = (int)(Z / Epsilon);

                return HashCode.Combine(Id, xHash, yHash, zHash);
            }

            // Ïåðåãðóçêà îïåðàòîðîâ == è !=
            public static bool operator ==(LiraNodeInfo left, LiraNodeInfo right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(LiraNodeInfo left, LiraNodeInfo right)
            {
                return !(left == right);
            }
        }

        public readonly struct LiraElementInfo
        {
            public int Id { get; }
            public int[] NodeIds { get; } // Óçëû, ïðèíàäëåæàùèå ýëåìåíòó

            public LiraElementInfo(int id, int[] nodeIds)
            {
                Id = id;
                NodeIds = nodeIds ?? [];
            }
            public static LiraElementInfo Empty => new(0, []);
            public bool IsEmpty => Id == 0;
        }

        public class Plaque
        {
            public int Id { get; set; } = 0;
            public List<MidasElementInfo> Elements { get; set; } = []; // Ýëåìåíòû, ïðèíàäëåæàùèå ïëèòå
            public List<MidasNodeInfo> Nodes { get; set; } = []; // Óçëû, ïðèíàäëåæàùèå ïëèòå
            public double RigidNodes { get; set; } = 0; // æåñòêîcòü äëÿ óçëîâ, ïðèíàäëåæàùèõ ýòîé ïëèòå

        }
    }
}

