﻿using System.Collections.Generic;
using Wren.Core.VM;

namespace Wren.Core.Objects
{
    public class ObjList : Obj
    {
        // The elements in the list.
        readonly List<Value> elements;

        // Creates a new list with [numElements] elements (which are left
        // uninitialized.)
        public ObjList(int numElements)
        {
            elements = new List<Value>(numElements);
            ClassObj = WrenVM.ListClass;
        }

        public void Clear()
        {
            elements.Clear();
        }

        public int Count()
        {
            return elements.Count;
        }

        public Value Get(int index)
        {
            return elements[index];
        }

        public void Set(Value v, int index)
        {
            elements[index] = v;
        }

        // Inserts [value] in [list] at [index], shifting down the other elements.
        public void Insert(Value c, int index)
        {
            elements.Insert(index, c);
        }

        public void Add(Value v)
        {
            elements.Add(v);
        }

        // Removes and returns the item at [index] from [list].
        public Value RemoveAt(int index)
        {
            if (elements.Count > index)
            {
                Value v = elements[index];
                elements.RemoveAt(index);
                return v;
            }
            return new Value (ValueType.Null);
        }

    }
}
