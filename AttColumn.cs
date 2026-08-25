using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;

namespace AlexGeospatial
{
    public class AttColumn
    {
        public string _Name;
        public GeospatialTools.eFieldType _efldType;
        public int _length;
        public int _decimal;
        public List<object> _rows;
        public AttColumn(string name, GeospatialTools.eFieldType typ, int len, int dec)
        {
            _Name = name;
            _efldType = typ;
            _length = len;
            _decimal = dec;
            var datatyp = getEFldType;
            _rows = new List<object>();
            // _rows = _rows.ToDictionary(Function(k) k.Key, Function(v) CTypeDynamic(v.Value, getEFldType))
        }
        public void recordVal(object val, long atInd)
        {
            string strVal = val.ToString();
            strVal = strVal.Substring(0, Math.Min(_length, strVal.Length));
            if (atInd == _rows.Count)
            {
                _rows.Add(new object());
            }
            var fitVal = default(object);            
            switch (_efldType)
            {
                case GeospatialTools.eFieldType.shpText:
                {
                    fitVal = strVal;
                    break;
                }
                case GeospatialTools.eFieldType.shpDouble:
                case GeospatialTools.eFieldType.shpFloat:
                {
                    double dval = 0;
                    if (double.TryParse(strVal, out dval))
                    {
                        fitVal = Math.Round(dval, _decimal);
                    }
                    break;
                }
                case GeospatialTools.eFieldType.shpBoolean:
                {
                    bool boolVal = false;
                    if (bool.TryParse(strVal, out boolVal))
                    {
                        fitVal = boolVal;
                    }
                    break;
                }
                case GeospatialTools.eFieldType.shpDate:
                {
                    DateTime dateval = new DateTime();
                    if (DateTime.TryParse(strVal, out dateval))
                    {
                        fitVal = dateval;
                    }
                    break;
                }
                case GeospatialTools.eFieldType.shpSingle:
                {
                    Single sval = 0;
                    if (Single.TryParse(strVal, out sval))
                    {
                        fitVal = Math.Round(sval, _decimal);
                    }
                    break;
                }
                case GeospatialTools.eFieldType.shpInteger:
                {
                    int intval = 0;
                    if (int.TryParse(strVal, out intval))
                    {
                        fitVal = intval;
                    }
                    break;
                }
                case GeospatialTools.eFieldType.shpLong:
                {
                    Int64 lintval = 0;
                    if (Int64.TryParse(strVal, out lintval))
                    {
                        fitVal = lintval;
                    }
                    break;
                }
            }
            _rows[(int)atInd] = fitVal;
        }
        public void fillRows(long rowCount)
        {
            for (long i = 0L, loopTo = rowCount - 1L; i <= loopTo; i++)
                _rows.Add(new object());
        }
        public Type getEFldType
        {
            get
            {
                var outTyp = default(Type);
                switch (_efldType)
                {
                    case GeospatialTools.eFieldType.shpBoolean:
                    {
                        outTyp = typeof(bool);
                        break;
                    }
                    case GeospatialTools.eFieldType.shpDate:
                    {
                        outTyp = typeof(DateTime);
                        break;
                    }
                    case GeospatialTools.eFieldType.shpDouble:
                    {
                        outTyp = typeof(double);
                        break;
                    }
                    case GeospatialTools.eFieldType.shpFloat:
                    {
                        outTyp = typeof(double);
                        break;
                    }
                    case GeospatialTools.eFieldType.shpInteger:
                    {
                        outTyp = typeof(int);
                        break;
                    }
                    case GeospatialTools.eFieldType.shpLong:
                    {
                        outTyp = typeof(long);
                        break;
                    }
                    case GeospatialTools.eFieldType.shpNumeric:
                    {
                        outTyp = typeof(double);
                        break;
                    }
                    case GeospatialTools.eFieldType.shpSingle:
                    {
                        outTyp = typeof(short);
                        break;
                    }
                    case GeospatialTools.eFieldType.shpText:
                    {
                        outTyp = typeof(string);
                        break;
                    }
                }
                return outTyp;
            }
        }
        public object getRowVal(long ind)
        {
            if (_rows[(int)ind] == null)
            {
                return "";
            }            
            else
            {
                return _rows[(int)ind];
            }
        }
    }
}
