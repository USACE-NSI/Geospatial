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
            if (atInd == _rows.Count)
            {
                _rows.Add(new object());
            }
            var fitVal = default(object);            
            switch (_efldType)
            {
                case GeospatialTools.eFieldType.shpText:
                {
                    fitVal = Strings.Left((string)val, _length);
                    break;
                }
                case GeospatialTools.eFieldType.shpDouble:
                case GeospatialTools.eFieldType.shpFloat:
                {
                    if (string.IsNullOrEmpty((string)val))
                    {
                        fitVal = 0;
                    }                   
                    //else if (val.ToString().Contains("E"))
                    //{
                    //    string Fcode = "F" + _decimal;
                    //    string stringVal = Strings.Left(NSI2Gen_2021.GeneralTools.convertFromSciNotation(Conversions.ToDouble(val)).ToString(Fcode), Math.Min(val.ToString().Length, _length + _decimal));
                    //    fitVal = stringVal;
                    //}
                    else
                    {
                        double dval = 0;
                        if(double.TryParse(val.ToString(), out dval))
                        {
                            fitVal = Strings.Left(Math.Round(dval, _decimal).ToString(), Math.Min(val.ToString().Length, _length + _decimal));
                        }                        
                    }
                    break;
                }
                case GeospatialTools.eFieldType.shpBoolean:
                {
                    if (string.IsNullOrEmpty(val.ToString()))
                    {
                        fitVal = false;
                    }
                    else
                    {
                        fitVal = val.ToString();
                    }
                    break;
                }
                case GeospatialTools.eFieldType.shpDate:
                {
                    if (string.IsNullOrEmpty(val.ToString()))
                    {
                        fitVal = DateTime.MinValue;
                    }
                    else
                    {
                        fitVal = Strings.Left(val.ToString(), _length);
                    }
                    break;
                }
                case GeospatialTools.eFieldType.shpSingle:
                {
                    if (string.IsNullOrEmpty(val.ToString()))
                    {
                        fitVal = 0;
                    }
                    else
                    {
                        fitVal = Strings.Left(val.ToString(), _length);
                    }

                    break;
                }
                case GeospatialTools.eFieldType.shpInteger:
                {
                    if (string.IsNullOrEmpty(val.ToString()))
                    {
                        fitVal = 0;
                    }
                    else
                    {
                        fitVal = Strings.Left(val.ToString(), _length);
                    }

                    break;
                }
                case GeospatialTools.eFieldType.shpLong:
                {
                    if (string.IsNullOrEmpty(val.ToString()))
                    {
                        fitVal = 0;
                    }
                    else
                    {
                        fitVal = Strings.Left(val.ToString(), _length);
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
