using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlexGeospatial.RTree
{
    public class RTreeManager
    {
        public RTreeNode _root;
        private int _minChildren;
        private int _maxChildren;       
        public RTreeManager(int minChilds = 4, int maxChilds = 10)
        {
            _minChildren = minChilds;
            _maxChildren = maxChilds;
            _root = new RTreeNode(this, _maxChildren, _minChildren);            
        }

        public void addFeature(int[] featInd, double mbrXmax, double mbrXmin, double mbrYmax, double mbrYmin)
        {
            RTreeNode featNode = new RTreeNode(this, _maxChildren, _minChildren, featInd);
            featNode.MBRXMin = mbrXmin;
            featNode.MBRXMax = mbrXmax;
            featNode.MBRYMin = mbrYmin;
            featNode.MBRYMax = mbrYmax;
            _root.addFeatureChild(featNode);
        }
        public List<RTreeNode> findByXY(double x, double y)
        {
            List<RTreeNode> nodePAth = new List<RTreeNode>();
            _root.getCandidateFeatNodesByMBR(x, x, y, y, nodePAth);            
            return nodePAth;
        }
        public List<RTreeNode> findByInd(int ind)
        {
            List<RTreeNode> nodePAth = new List<RTreeNode>();
            _root.getChildrenContainingInd(ind, nodePAth);
            return nodePAth;
        }       
        public List<RTreeNode> getEndNodes
        {
            get
            {
                List<RTreeNode> endNodes = new List<RTreeNode>();
                _root.getEndNodes(endNodes);
                return endNodes;
            }
        }
    }
}
