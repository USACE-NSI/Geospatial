using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Spatial
{
    public class RTreeNode
    {
        public RTreeNode _parent;
        public RTreeManager _treeManager;
        public List<RTreeNode> _children = new List<RTreeNode>();  
        public int[] _featureIndex; //  { Feature Index, Sub-Part Index (for holes, etc) }
        public int maxChidrens = 0;
        public int minChidrens = 0;

        public BoundingBox BoundingBox {get;set;} = BoundingBox.Empty;

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
                _parent.addChild(newKidsOntheBlock[0], false, true);
                _parent.addChild(newKidsOntheBlock[1], true, true);                
            }
            else
            {
                RTreeNode newRoot = new RTreeNode(_treeManager, maxChidrens, minChidrens);
                newKidsOntheBlock[0].UpdateParents(newRoot);
                newKidsOntheBlock[1].UpdateParents(newRoot);
                newRoot.addChild(newKidsOntheBlock[0], false, true);
                newRoot.addChild(newKidsOntheBlock[1], true, true);        
                _treeManager._root = newRoot;                
            }    
        }
        private void buildChildOptions(List<(RTreeNode[], double[])> options, bool xAxis, bool min)
        {            
            List<RTreeNode> sortedChidrens = null;
            if(xAxis)
            {
                if (min) { sortedChidrens = _children.OrderBy(c => c.BoundingBox.MinX).ToList(); }                
                else { sortedChidrens = _children.OrderBy(c => c.BoundingBox.MaxX).ToList(); }
            }
            else
            {
                if (min) { sortedChidrens = _children.OrderBy(c => c.BoundingBox.MinY).ToList(); }
                else { sortedChidrens = _children.OrderBy(c => c.BoundingBox.MaxY).ToList(); }
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
                        node1.addChild(Child, false, false);
                    }
                    else
                    {
                        node2.addChild(Child, false, false);
                    }                   
                }
                double overlapWidth = Math.Max(0, Math.Min(node1.BoundingBox.MaxX, node2.BoundingBox.MaxX) - Math.Max(node1.BoundingBox.MinX, node2.BoundingBox.MinX));
                double overlapHeight = Math.Max(0, Math.Min(node1.BoundingBox.MaxY, node2.BoundingBox.MaxY) - Math.Max(node1.BoundingBox.MinY, node2.BoundingBox.MinY));
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
                addChild(feature, true, true); 
            }            
            else
            {
                RTreeNode bestCandidate = null;
                double minExtension = double.MaxValue;
                foreach (RTreeNode childnode in _children)
                {
                    double extensionReq = childnode.getAddedSizeToAccomodate(feature.BoundingBox);
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
            getCandidateEndNodesByMBR(feature.BoundingBox, candidateKids);
            if (candidateKids.Count == 0) { candidateKids = _treeManager.getEndNodes; }
            RTreeNode bestCandidate = null;
            double minExtension = double.MaxValue;
            foreach (RTreeNode candidate in candidateKids)
            {
                double extensionReq = candidate.getAddedSizeToAccomodate(feature.BoundingBox);
                if (extensionReq < minExtension)
                {
                    bestCandidate = candidate;
                    minExtension = extensionReq;
                }
            }
            bestCandidate.addChild(feature, true, true);
        }
        public void addChild(RTreeNode child, bool canSplit, bool canPropagateMBRup) 
        {
            _children.Add(child);
            child._parent = this;
            if(child.BoundingBox.MinX < this.BoundingBox.MinX) { this.BoundingBox.MinX = child.BoundingBox.MinX; }
            if(child.BoundingBox.MaxX > this.BoundingBox.MaxX) { this.BoundingBox.MaxX = child.BoundingBox.MaxX; }
            if(child.BoundingBox.MinY < this.BoundingBox.MinY) { this.BoundingBox.MinY = child.BoundingBox.MinY; }
            if(child.BoundingBox.MaxY > this.BoundingBox.MaxY) { this.BoundingBox.MaxY = child.BoundingBox.MaxY; }
            if(_children.Count > maxChidrens && canSplit)
            {
                split();
            }
            else if(canPropagateMBRup)
            {
                RecomputeMBR();
            }
        }
        public void RecomputeMBR()
        {
            BoundingBox.MinX = _children.Min(c => c.BoundingBox.MinX);
            BoundingBox.MaxX = _children.Max(c => c.BoundingBox.MaxX);
            BoundingBox.MinY = _children.Min(c => c.BoundingBox.MinY);
            BoundingBox.MaxY = _children.Max(c => c.BoundingBox.MaxY);
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
        public void getCandidateEndNodesByMBR(BoundingBox bbox, List<RTreeNode> nodeWalk)
        {
            if (getIsEndNode)
            {
                nodeWalk.Add(this);
            }
            else
            {
                foreach (RTreeNode node in _children)
                {
                    if (node.getMBRoverlap(bbox) > 0)
                    {
                        node.getCandidateEndNodesByMBR(bbox, nodeWalk);
                    }
                }
            }
        }
        public void getCandidateFeatNodesByMBR(BoundingBox bbox, List<RTreeNode> nodeWalk)
        {
            if (getIsEndNode)
            {
                foreach (RTreeNode node in _children)
                {
                    if (node.getMBRoverlap(bbox) > 0)
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
                    if (node.getMBRoverlap(bbox) > 0)
                    {
                        node.getCandidateFeatNodesByMBR(bbox, nodeWalk);
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
        public double getMBRoverlap(BoundingBox bbox) 
        {
            double overlap = 0;
            if ((bbox.MaxX >= BoundingBox.MinX && bbox.MaxX <= BoundingBox.MaxX) || (bbox.MinX >= BoundingBox.MinX && bbox.MinX <= BoundingBox.MaxX))
            {
                if ((bbox.MaxY >= BoundingBox.MinY && bbox.MaxY <= BoundingBox.MaxY) || (bbox.MinY >= BoundingBox.MinY && bbox.MinY <= BoundingBox.MaxY))
                {
                    double xAxisOverlap = Math.Min(bbox.MaxX, BoundingBox.MaxX) - Math.Max(bbox.MinX, BoundingBox.MinX);
                    double YAxisOverlap = Math.Min(bbox.MaxY, BoundingBox.MaxY) - Math.Max(bbox.MinY, BoundingBox.MinY);
                    overlap = Math.Max(xAxisOverlap * YAxisOverlap, 1); //Always return at least one, if top two conditions are met to avoid ignoring point shape overlap
                }
            }
            return overlap;
        }
        public double getAddedSizeToAccomodate(BoundingBox bbox) 
        {
            double featArea = (bbox.MaxX - bbox.MinX) * (bbox.MaxY - bbox.MinY);
            return getArea + featArea - getMBRoverlap(bbox);
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
                if (BoundingBox.MaxX < BoundingBox.MinX || BoundingBox.MaxY < BoundingBox.MinY) { return 0; }
                return (BoundingBox.MaxX - BoundingBox.MinX) * (BoundingBox.MaxY - BoundingBox.MinY);
            }
        }
        public double getPerimeter
        {
            get { return 2 * ((BoundingBox.MaxX - BoundingBox.MinX) + (BoundingBox.MaxY - BoundingBox.MinY)); }
        }
    }
}
