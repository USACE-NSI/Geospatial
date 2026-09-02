using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Spatial;

public class RTreeManager
{
  public RTreeNode Root { get; set; }
  private int MinChildren { get; set; }
  private int MaxChildren { get; set; }

  public RTreeManager(int minChilds = 4, int maxChilds = 10)
  {
    MinChildren = minChilds;
    MaxChildren = maxChilds;
    Root = new RTreeNode(this, MaxChildren, MinChildren);
  }

  public void addFeature(int[] featInd, BoundingBox bbox)
  {
    RTreeNode featNode = new(this, MaxChildren, MinChildren, featInd);
    featNode.BoundingBox = bbox;
    Root.addFeatureChildEnforceIntersect(featNode);
  }

  public List<RTreeNode> findByXY(double x, double y)
  {
    List<RTreeNode> nodePAth = new();
    BoundingBox bbox = new(x, y, x, y);
    Root.getCandidateFeatNodesByMBR(bbox, nodePAth);
    return nodePAth;
  }

  public List<RTreeNode> findByInd(int ind)
  {
    List<RTreeNode> nodePAth = new();
    Root.getChildrenContainingInd(ind, nodePAth);
    return nodePAth;
  }

  public List<RTreeNode> getEndNodes
  {
    get
    {
      List<RTreeNode> endNodes = new();
      Root.getEndNodes(endNodes);
      return endNodes;
    }
  }
}
