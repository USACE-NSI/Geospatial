using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;

namespace AlexGeospatial
{
    public class AttTbl
    {
        public Dictionary<string, AttColumn> _columns;
        public AttTbl()
        {
            _columns = new Dictionary<string, AttColumn>();
        }
        //public void addAppendColData(Field @field, long atInd)
        //{
        //    if (!_columns.ContainsKey(@field.Name))
        //    {
        //        _columns.Add(@field.Name, new AttColumn(@field.Name, @field.Type, @field.Size, @field.Decimal));
        //    }
        //    _columns[@field.Name].recordVal(@field.Value, atInd);
        //}
        public void addAppendColData(string fieldName, GeospatialTools.eFieldType fieldType, int fieldSize, int fieldDec, object val, long atind)
        {
            if (!_columns.ContainsKey(fieldName))
            {
                _columns.Add(fieldName, new AttColumn(fieldName, fieldType, fieldSize, fieldDec));
            }
            _columns[fieldName].recordVal(val, atind);
        }
        //public void addAppendColData(AttColumn @field, long fromInd, long atInd, string newName = "")
        //{
        //    if (string.IsNullOrEmpty(newName))
        //        newName = @field._Name;
        //    if (!_columns.ContainsKey(newName))
        //    {
        //        _columns.Add(newName, new AttColumn(newName, @field._efldType, @field._length, @field._decimal));
        //    }
        //    _columns[newName].recordVal(@field.getRowVal(fromInd), atInd);
        //}
        public void writeColData(string fieldname, object val, long atInd)
        {
            _columns[fieldname].recordVal(val, atInd);
        }
        public void AddField(string name, GeospatialTools.eFieldType typ, int len, int dec)
        {
            if (!_columns.ContainsKey(name))
            {
                _columns.Add(name, new AttColumn(name, typ, len, dec));
                _columns[name].fillRows(getRowCount);
            }
        }
        public void RemoveField(string name)
        {
            if (_columns.ContainsKey(name))
            {
                _columns.Remove(name);
            }
        }
        public void AddRow()
        {
            foreach (AttColumn col in _columns.Values)
                col._rows.Add(new object());
        }
        public void RemoveRow(long featInd)
        {
            foreach (var col in _columns)
                col.Value._rows.RemoveAt((int)featInd);
        }
        public T getRowValOf<T>(string colName, long ind)
        {           
            object rawVal = _columns[colName].getRowVal(ind);
            return (T)Convert.ChangeType(rawVal, typeof(T));
        }
        public string getRowValAsString(string colName, long ind)
        {
            return getRowValOf<string>(colName, ind).ToString();
        }       
        public void reorderCols(Dictionary<string, int> map)
        {
            var sortedCols = _columns.OrderBy(x => map[x.Key]).ToDictionary(k => k.Key, v => v.Value);
            _columns = sortedCols;
        }
        public void renameCol(object Colname, object newname)
        {
            if (_columns.ContainsKey(Colname.ToString()))
            {
                var colval = _columns[Colname.ToString()];
                colval._Name = newname.ToString();
                _columns.Remove(colval._Name);
                _columns.Add(colval._Name, colval);
            }            
        }
        public Dictionary<string, object> get_getFieldDataAtInd(long ind)
        {
            var fields = new Dictionary<string, object>();
            foreach (var col in _columns)
                fields.Add(col.Key, getRowValOf<object>(col.Key, ind));
            return fields;
        }
        public long getRowCount
        {
            get
            {
                return _columns.Max(x => x.Value._rows.Count);
            }
        }
    }
}
