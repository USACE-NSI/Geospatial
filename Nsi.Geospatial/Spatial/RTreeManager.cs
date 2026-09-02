using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Spatial;

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

      public void addFeature(int[] featInd, BoundingBox bbox)
      {
          RTreeNode featNode = new RTreeNode(this, _maxChildren, _minChildren, featInd);
          featNode.BoundingBox = bbox;
          _root.addFeatureChildEnforceIntersect(featNode);
      }
      public List<RTreeNode> findByXY(double x, double y)
      {
          List<RTreeNode> nodePAth = new List<RTreeNode>();
          BoundingBox bbox = new BoundingBox(x,y,x,y);
          _root.getCandidateFeatNodesByMBR(bbox, nodePAth);            
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