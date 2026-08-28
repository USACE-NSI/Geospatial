using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AlexGeospatial.RTree
{
    public class RTreeNode
    {
        public RTreeNode _parent;
        public RTreeManager _treeManager;
        public List<RTreeNode> _children = new List<RTreeNode>();  
        public int[] _featureIndex; //  { Feature Index, Sub-Part Index (for holes, etc) }
        public int maxChidrens = 0;
        public int minChidrens = 0;

        public double MBRXMin { get; set; } = double.MaxValue;
        public double MBRXMax { get; set; } = double.MinValue;
        public double MBRYMin { get; set; } = double.MaxValue;
        public double MBRYMax { get; set; } = double.MinValue;

        public double cumulativeOverlap { get; set; }      
        public double siblingOverlap { get; set; }

        public RTreeNode(RTreeManager treemanager, int maxChildren, int minChildren, int[] featInd = null)
        {
            _treeManager = treemanager;
            maxChidrens = maxChildren;
            minChidrens = minChildren;
            _featureIndex = featInd;
        }
        public void split()
        {
            List<(RTreeNode[] nodes, double[] metrics)> Options = new List<(RTreeNode[], double[])>();            

            //First X axis split by min
            buildChildOptions(Options, true, true);

            //Then X axis split by max
            buildChildOptions(Options, true, false);

            //Then Y axis split by min
            buildChildOptions(Options, false, true);

            //Then Y axis split by max
            buildChildOptions(Options, false, false);

            List<RTreeNode> newKidsOntheBlock;
            
            newKidsOntheBlock = Options.OrderBy(x => x.metrics[0]).ThenBy(x => x.metrics[1]).ThenBy(x => x.metrics[2]).First().nodes.ToList();            
            
            if(_parent != null)
            {
                _parent._children.Remove(this);
                newKidsOntheBlock[0].UpdateParents(_parent);
                newKidsOntheBlock[1].UpdateParents(_parent);
                _parent.addChild(newKidsOntheBlock[0], true);
                _parent.addChild(newKidsOntheBlock[1], false);                
            }
            else
            {
                RTreeNode newRoot = new RTreeNode(_treeManager, maxChidrens, minChidrens);
                newKidsOntheBlock[0].UpdateParents(newRoot);
                newKidsOntheBlock[1].UpdateParents(newRoot);
                newRoot.addChild(newKidsOntheBlock[0], true);
                newRoot.addChild(newKidsOntheBlock[1], false);        
                _treeManager._root = newRoot;
            }    
        }
        private void buildChildOptions(List<(RTreeNode[], double[])> options, bool xAxis, bool min)
        {            
            List<RTreeNode> sortedChidrens = null;
            if(xAxis)
            {
                if (min) { sortedChidrens = _children.OrderBy(c => c.MBRXMin).ToList(); }                
                else { sortedChidrens = _children.OrderBy(c => c.MBRXMax).ToList(); }
            }
            else
            {
                if (min) { sortedChidrens = _children.OrderBy(c => c.MBRYMin).ToList(); }
                else { sortedChidrens = _children.OrderBy(c => c.MBRYMax).ToList(); }
            }
                       
            for(int split = minChidrens; split <= _children.Count() - minChidrens; split++)
            {
                RTreeNode node1 = new RTreeNode(_treeManager, maxChidrens, minChidrens);
                RTreeNode node2 = new RTreeNode(_treeManager, maxChidrens, minChidrens);
                for (int i = 0; i < sortedChidrens.Count; i++)
                {
                    var Child = sortedChidrens[i];
                    if (i < split)
                    {
                        node1.addChild(Child, true);
                    }
                    else
                    {
                        node2.addChild(Child, true);
                    }                   
                }
                double overlapWidth = Math.Max(0, Math.Min(node1.MBRXMax, node2.MBRXMax) - Math.Max(node1.MBRXMin, node2.MBRXMin));
                double overlapHeight = Math.Max(0, Math.Min(node1.MBRYMax, node2.MBRYMax) - Math.Max(node1.MBRYMin, node2.MBRYMin));
                double overlap = overlapWidth * overlapHeight;
                double totalArea = node1.getArea + node2.getArea;
                double perimeterTotal = node1.getPerimeter + node2.getPerimeter;

                node1.siblingOverlap = overlap;
                node2.siblingOverlap = overlap;
                node1.cumulativeOverlap = overlap + siblingOverlap;
                node2.cumulativeOverlap = overlap + siblingOverlap;               

                options.Add((new RTreeNode[] { node1, node2 }, new double[] { overlap, totalArea, perimeterTotal }));
            }
        }
        public void addFeatureChild(RTreeNode feature)
        {
            if(getIsEndNode) 
            { 
                addChild(feature, false); 
            }            
            else
            {
                RTreeNode bestCandidate = null;
                double minExtension = double.MaxValue;
                foreach (RTreeNode childnode in _children)
                {
                    double extensionReq = childnode.getAddedSizeToAccomodate(feature.MBRXMax, feature.MBRXMin, feature.MBRYMax, feature.MBRYMin);
                    if (extensionReq < minExtension)
                    {
                        bestCandidate = childnode;
                        minExtension = extensionReq;
                    }
                    else if (extensionReq == minExtension && childnode.getArea < bestCandidate.getArea)
                    {
                        bestCandidate = childnode;
                    }
                }
                bestCandidate.addFeatureChild(feature);
            }            
        }
        public void addFeatureChildEnforceIntersect(RTreeNode feature)
        {
            //Find the lowest level child node that would least expand to accept the feature geometry
            List<RTreeNode> candidateKids = new List<RTreeNode>();
            getCandidateEndNodesByMBR(feature.MBRXMax, feature.MBRXMin, feature.MBRYMax, feature.MBRYMin, candidateKids);
            if (candidateKids.Count == 0) { candidateKids = _treeManager.getEndNodes; }
            RTreeNode bestCandidate = null;
            double minExtension = double.MaxValue;
            foreach (RTreeNode candidate in candidateKids)
            {
                double extensionReq = candidate.getAddedSizeToAccomodate(feature.MBRXMax, feature.MBRXMin, feature.MBRYMax, feature.MBRYMin);
                if (extensionReq < minExtension)
                {
                    bestCandidate = candidate;
                    minExtension = extensionReq;
                }
            }
            bestCandidate.addChild(feature, false);
        }
        public void addChild(RTreeNode child, bool evaluation) 
        {
            _children.Add(child);
            child._parent = this;
            if(child.MBRXMin < MBRXMin) { MBRXMin = child.MBRXMin; }
            if(child.MBRXMax > MBRXMax) { MBRXMax = child.MBRXMax; }
            if(child.MBRYMin < MBRYMin) { MBRYMin = child.MBRYMin; }
            if(child.MBRYMax > MBRYMax) { MBRYMax = child.MBRYMax; }
            if(_children.Count > maxChidrens && !evaluation)
            {
                split();
            }
        }
        public void RecomputeMBR()
        {
            MBRXMin = _children.Min(c => c.MBRXMin);
            MBRXMax = _children.Max(c => c.MBRXMax);
            MBRYMin = _children.Min(c => c.MBRYMin);
            MBRYMax = _children.Max(c => c.MBRYMax);
            if(_parent != null) { _parent.RecomputeMBR(); }
        }
        public void UpdateParents(RTreeNode newParent)
        {
            _parent = newParent;
            foreach (var child in _children)
            {
                child.UpdateParents(this);
            }
        }
        public void getEndNodes(List<RTreeNode> nodeWalk)
        {
            if(getIsEndNode)
            {
                nodeWalk.Add(this);
            }
            else
            {
                foreach (RTreeNode node in _children)
                {                    
                    node.getEndNodes(nodeWalk);                    
                }
            }                
        }
        public void getCandidateEndNodesByMBR(double XMax, double XMin, double YMax, double YMin, List<RTreeNode> nodeWalk)
        {
            if (getIsEndNode)
            {
                nodeWalk.Add(this);
            }
            else
            {
                foreach (RTreeNode node in _children)
                {
                    if (node.getMBRoverlap(XMax, XMin, YMax, YMin) > 0)
                    {
                        node.getCandidateEndNodesByMBR(XMax, XMin, YMax, YMin, nodeWalk);
                    }
                }
            }
        }
        public void getCandidateFeatNodesByMBR(double XMax, double XMin, double YMax, double YMin, List<RTreeNode> nodeWalk)
        {
            if (getIsEndNode)
            {
                foreach (RTreeNode node in _children)
                {
                    if (node.getMBRoverlap(XMax, XMin, YMax, YMin) > 0)
                    {
                        nodeWalk.Add(this);
                        break;
                    }
                }
            }
            else
            {
                foreach (RTreeNode node in _children)
                {
                    if (node.getMBRoverlap(XMax, XMin, YMax, YMin) > 0)
                    {
                        node.getCandidateFeatNodesByMBR(XMax, XMin, YMax, YMin, nodeWalk);
                    }
                }
            }
        }        
        public void getChildrenContainingInd(int ind, List<RTreeNode> nodeWalk)
        {
            if (getIsEndNode)
            {
                foreach (RTreeNode node in _children)
                {
                    if (node._featureIndex[0] == ind)
                    {
                        getPathReverse(nodeWalk);
                        break;
                    }
                }                
            }
            else
            {
                foreach (RTreeNode node in _children)
                {
                    node.getChildrenContainingInd(ind, nodeWalk);
                }
            }   
        }
        public void getPathReverse(List<RTreeNode> nodeWalk)
        {
            nodeWalk.Add(this);
            if (_parent != null)
            {
                _parent.getPathReverse(nodeWalk);
            }
        }
        public double getMBRoverlap(double XMax, double XMin, double YMax, double YMin) 
        {
            double overlap = 0;
            if ((XMax >= MBRXMin && XMax <= MBRXMax) || (XMin >= MBRXMin && XMin <= MBRXMax))
            {
                if ((YMax >= MBRYMin && YMax <= MBRYMax) || (YMin >= MBRYMin && YMin <= MBRYMax))
                {
                    double xAxisOverlap = Math.Min(XMax, MBRXMax) - Math.Max(XMin, MBRXMin);
                    double YAxisOverlap = Math.Min(YMax, MBRYMax) - Math.Max(YMin, MBRYMin);
                    overlap = Math.Max(xAxisOverlap * YAxisOverlap, 1); //Always return at least one, if top two conditions are met to avoid ignoring point shape overlap
                }
            }
            return overlap;
        }
        public double getAddedSizeToAccomodate(double XMax, double XMin, double YMax, double YMin) 
        {
            double featArea = (XMax - XMin) * (YMax - YMin);
            return getArea + featArea - getMBRoverlap(XMax, XMin, YMax, YMin);
        }
        public bool getIsEndNode
        {
            get
            {
                if ((_children.Count == 0 && _featureIndex == null) || (_children.Count > 0 && _children[0]._featureIndex != null))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        public double getArea
        {
            get
            {
                if (MBRXMax < MBRXMin || MBRYMax < MBRYMin) { return 0; }
                return (MBRXMax - MBRXMin) * (MBRYMax - MBRYMin);
            }
        }
        public double getPerimeter
        {
            get { return 2 * ((MBRXMax - MBRXMin) + (MBRYMax - MBRYMin)); }
        }
    }
}
