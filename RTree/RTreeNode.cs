using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public RTreeNode(RTreeManager treemanager, int maxChildren, int minChildren, int[] featInd = null)
        {
            _treeManager = treemanager;
            maxChidrens = maxChildren;
            minChidrens = minChildren;
            _featureIndex = featInd;
        }
        public void split()
        {
            List<(RTreeNode[] nodes, double[] metrics)> xOptions = new List<(RTreeNode[], double[])>();
            List<(RTreeNode[] nodes, double[] metrics)> yOptions = new List<(RTreeNode[], double[])>();

            //First X axis split by min
            buildChildOptions(xOptions, true, true);

            //Then X axis split by max
            buildChildOptions(xOptions, true, false);

            //Then Y axis split by min
            buildChildOptions(yOptions, false, true);

            //Then Y axis split by max
            buildChildOptions(yOptions, false, false);

            double xMarginSum = xOptions.Sum(x => x.metrics[2]);
            double yMarginSum = yOptions.Sum(x => x.metrics[2]);

            List<RTreeNode> newKidsOntheBlock;
            if (xMarginSum < yMarginSum)
            {
                newKidsOntheBlock = xOptions.OrderBy(x => x.metrics[0]).ThenBy(x => x.metrics[1]).ThenBy(x => x.metrics[2]).First().nodes.ToList();
            }
            else
            {
                newKidsOntheBlock = yOptions.OrderBy(x => x.metrics[0]).ThenBy(x => x.metrics[1]).ThenBy(x => x.metrics[2]).First().nodes.ToList();
            }
                            
            if(_parent != null)
            {
                _parent._children.Remove(this);
                foreach (var child in newKidsOntheBlock)
                {
                    _parent.addChild(child, false);
                }
            }
            else
            {
                RTreeNode newRoot = new RTreeNode(_treeManager, maxChidrens, minChidrens);
                foreach (var child in newKidsOntheBlock)
                {
                    newRoot.addChild(child, false);
                }
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

                options.Add((new RTreeNode[] { node1, node2 }, new double[] { overlap, totalArea, perimeterTotal }));
            }
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
        public double getArea
        {
            get { return (MBRXMax - MBRXMin) * (MBRYMax - MBRYMin); }
        }
        public double getPerimeter
        {
            get { return 2 * ((MBRXMax - MBRXMin) + (MBRYMax - MBRYMin)); }
        }
    }
}
