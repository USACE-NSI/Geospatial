using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Spatial;

public class RTreeNode
{
  public RTreeNode Parent { get; private set; }
  public RTreeManager TreeManager { get; private set; }
  public List<RTreeNode> Children { get; private set; } = new List<RTreeNode>();
  public int[] FeatureIndex { get; private set; } //  { Feature Index, Sub-Part Index (for holes, etc) }
  public int MaxChidrens { get; private set; } = 0;
  public int MinChidrens { get; private set; } = 0;

  public BoundingBox BoundingBox { get; set; } = BoundingBox.Empty;

  public double cumulativeOverlap { get; set; }
  public double siblingOverlap { get; set; }

  public RTreeNode(RTreeManager treemanager, int maxChildren, int minChildren, int[] featInd = null)
  {
    TreeManager = treemanager;
    MaxChidrens = maxChildren;
    MinChidrens = minChildren;
    FeatureIndex = featInd;
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

    newKidsOntheBlock = Options
      .OrderBy(x => x.metrics[0])
      .ThenBy(x => x.metrics[1])
      .ThenBy(x => x.metrics[2])
      .First()
      .nodes.ToList();

    if (Parent != null)
    {
      Parent.Children.Remove(this);
      newKidsOntheBlock[0].UpdateParents(Parent);
      newKidsOntheBlock[1].UpdateParents(Parent);
      Parent.addChild(newKidsOntheBlock[0], false, true);
      Parent.addChild(newKidsOntheBlock[1], true, true);
    }
    else
    {
      RTreeNode newRoot = new(TreeManager, MaxChidrens, MinChidrens);
      newKidsOntheBlock[0].UpdateParents(newRoot);
      newKidsOntheBlock[1].UpdateParents(newRoot);
      newRoot.addChild(newKidsOntheBlock[0], false, true);
      newRoot.addChild(newKidsOntheBlock[1], true, true);
      TreeManager._root = newRoot;
    }
  }

  private void buildChildOptions(List<(RTreeNode[], double[])> options, bool xAxis, bool min)
  {
    List<RTreeNode> sortedChidrens = null;
    if (xAxis)
    {
      if (min)
      {
        sortedChidrens = Children.OrderBy(c => c.BoundingBox.MinX).ToList();
      }
      else
      {
        sortedChidrens = Children.OrderBy(c => c.BoundingBox.MaxX).ToList();
      }
    }
    else
    {
      if (min)
      {
        sortedChidrens = Children.OrderBy(c => c.BoundingBox.MinY).ToList();
      }
      else
      {
        sortedChidrens = Children.OrderBy(c => c.BoundingBox.MaxY).ToList();
      }
    }

    for (int split = MinChidrens; split <= Children.Count() - MinChidrens; split++)
    {
      RTreeNode node1 = new(TreeManager, MaxChidrens, MinChidrens);
      RTreeNode node2 = new(TreeManager, MaxChidrens, MinChidrens);
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
      double overlapWidth = Math.Max(
        0,
        Math.Min(node1.BoundingBox.MaxX, node2.BoundingBox.MaxX)
          - Math.Max(node1.BoundingBox.MinX, node2.BoundingBox.MinX)
      );
      double overlapHeight = Math.Max(
        0,
        Math.Min(node1.BoundingBox.MaxY, node2.BoundingBox.MaxY)
          - Math.Max(node1.BoundingBox.MinY, node2.BoundingBox.MinY)
      );
      double overlap = overlapWidth * overlapHeight;
      double totalArea = node1.getArea + node2.getArea;
      double perimeterTotal = node1.getPerimeter + node2.getPerimeter;

      node1.siblingOverlap = overlap;
      node2.siblingOverlap = overlap;
      node1.cumulativeOverlap = overlap + siblingOverlap;
      node2.cumulativeOverlap = overlap + siblingOverlap;

      options.Add(
        (new RTreeNode[] { node1, node2 }, new double[] { overlap, totalArea, perimeterTotal })
      );
    }
  }

  public void addFeatureChild(RTreeNode feature)
  {
    if (getIsEndNode)
    {
      addChild(feature, true, true);
    }
    else
    {
      RTreeNode bestCandidate = null;
      double minExtension = double.MaxValue;
      foreach (RTreeNode childnode in Children)
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
    List<RTreeNode> candidateKids = new();
    getCandidateEndNodesByMBR(feature.BoundingBox, candidateKids);
    if (candidateKids.Count == 0)
    {
      candidateKids = TreeManager.getEndNodes;
    }
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
    bestCandidate ??= TreeManager._root;
    bestCandidate.addChild(feature, true, true);
  }

  public void addChild(RTreeNode child, bool canSplit, bool canPropagateMBRup)
  {
    Children.Add(child);
    child.Parent = this;
    BoundingBox = BoundingBox.Union(child.BoundingBox);
    if (Children.Count > MaxChidrens && canSplit)
      split();
    else if (canPropagateMBRup)
      RecomputeMBR();
  }

  public void RecomputeMBR()
  {
    BoundingBox = new BoundingBox(
      Children.Min(c => c.BoundingBox.MinX),
      Children.Min(c => c.BoundingBox.MinY),
      Children.Max(c => c.BoundingBox.MaxX),
      Children.Max(c => c.BoundingBox.MaxY)
    );
    if (Parent != null)
      Parent.RecomputeMBR();
  }

  public void UpdateParents(RTreeNode newParent)
  {
    Parent = newParent;
    foreach (var child in Children)
    {
      child.UpdateParents(this);
    }
  }

  public void getEndNodes(List<RTreeNode> nodeWalk)
  {
    if (getIsEndNode)
    {
      nodeWalk.Add(this);
    }
    else
    {
      foreach (RTreeNode node in Children)
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
      foreach (RTreeNode node in Children)
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
      foreach (RTreeNode node in Children)
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
      foreach (RTreeNode node in Children)
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
      foreach (RTreeNode node in Children)
      {
        if (node.FeatureIndex[0] == ind)
        {
          getPathReverse(nodeWalk);
          break;
        }
      }
    }
    else
    {
      foreach (RTreeNode node in Children)
      {
        node.getChildrenContainingInd(ind, nodeWalk);
      }
    }
  }

  public void getPathReverse(List<RTreeNode> nodeWalk)
  {
    nodeWalk.Add(this);
    if (Parent != null)
    {
      Parent.getPathReverse(nodeWalk);
    }
  }

  public double getMBRoverlap(BoundingBox bbox)
  {
    double overlap = 0;
    if (
      (bbox.MaxX >= BoundingBox.MinX && bbox.MaxX <= BoundingBox.MaxX)
      || (bbox.MinX >= BoundingBox.MinX && bbox.MinX <= BoundingBox.MaxX)
    )
    {
      if (
        (bbox.MaxY >= BoundingBox.MinY && bbox.MaxY <= BoundingBox.MaxY)
        || (bbox.MinY >= BoundingBox.MinY && bbox.MinY <= BoundingBox.MaxY)
      )
      {
        double xAxisOverlap =
          Math.Min(bbox.MaxX, BoundingBox.MaxX) - Math.Max(bbox.MinX, BoundingBox.MinX);
        double YAxisOverlap =
          Math.Min(bbox.MaxY, BoundingBox.MaxY) - Math.Max(bbox.MinY, BoundingBox.MinY);
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
      if (
        (Children.Count == 0 && FeatureIndex == null)
        || (Children.Count > 0 && Children[0].FeatureIndex != null)
      )
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
      if (BoundingBox.MaxX < BoundingBox.MinX || BoundingBox.MaxY < BoundingBox.MinY)
      {
        return 0;
      }
      return (BoundingBox.MaxX - BoundingBox.MinX) * (BoundingBox.MaxY - BoundingBox.MinY);
    }
  }
  public double getPerimeter
  {
    get
    {
      return 2 * ((BoundingBox.MaxX - BoundingBox.MinX) + (BoundingBox.MaxY - BoundingBox.MinY));
    }
  }
}
