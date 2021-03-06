﻿using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Wren.Core.Objects;
using Wren.Core.VM;

namespace Wren.Core.Library
{
    class CoreLibrary
    {
        private readonly WrenVM _vm;

        private const string CoreLibSource =
            "class Bool {}\n"
            + "class Fiber {}\n"
            + "class Fn {}\n"
            + "class Foreign {}\n"
            + "class Null {}\n"
            + "class Num {}\n"
            + "\n"
            + "class Sequence {\n"
            + "  all(f) {\n"
            + "    var result = true\n"
            + "    for (element in this) {\n"
            + "      result = f.call(element)\n"
            + "      if (!result) return result\n"
            + "    }\n"
            + "    return result\n"
            + "  }\n"
            + "\n"
            + "  any(f) {\n"
            + "    var result = false\n"
            + "    for (element in this) {\n"
            + "      result = f.call(element)\n"
            + "      if (result) return result\n"
            + "    }\n"
            + "    return result\n"
            + "  }\n"
            + "\n"
            + "  contains(element) {\n"
            + "    for (item in this) {\n"
            + "      if (element == item) return true\n"
            + "    }\n"
            + "    return false\n"
            + "  }\n"
            + "\n"
            + "  count {\n"
            + "    var result = 0\n"
            + "    for (element in this) {\n"
            + "      result = result + 1\n"
            + "    }\n"
            + "    return result\n"
            + "  }\n"
            + "\n"
            + "  count(f) {\n"
            + "    var result = 0\n"
            + "    for (element in this) {\n"
            + "      if (f.call(element)) result = result + 1\n"
            + "    }\n"
            + "    return result\n"
            + "  }\n"
            + "\n"
            + "  each(f) {\n"
            + "    for (element in this) {\n"
            + "      f.call(element)\n"
            + "    }\n"
            + "  }\n"
            + "\n"
            + "  isEmpty { iterate(null) ? false : true }"
            + "\n"
            + "  map(transformation) { MapSequence.new(this, transformation) }\n"
            + "\n"
            + "  where(predicate) { WhereSequence.new(this, predicate) }\n"
            + "\n"
            + "  reduce(acc, f) {\n"
            + "    for (element in this) {\n"
            + "      acc = f.call(acc, element)\n"
            + "    }\n"
            + "    return acc\n"
            + "  }\n"
            + "\n"
            + "  reduce(f) {\n"
            + "    var iter = iterate(null)\n"
            + "    if (!iter) Fiber.abort(\"Can't reduce an empty sequence.\")\n"
            + "\n"
            + "    // Seed with the first element.\n"
            + "    var result = iteratorValue(iter)\n"
            + "    while (iter = iterate(iter)) {\n"
            + "      result = f.call(result, iteratorValue(iter))\n"
            + "    }\n"
            + "\n"
            + "    return result\n"
            + "  }\n"
            + "\n"
            + "  join() { join(\"\") }\n"
            + "\n"
            + "  join(sep) {\n"
            + "    var first = true\n"
            + "    var result = \"\"\n"
            + "\n"
            + "    for (element in this) {\n"
            + "      if (!first) result = result + sep\n"
            + "      first = false\n"
            + "      result = result + element.toString\n"
            + "    }\n"
            + "\n"
            + "    return result\n"
            + "  }\n"
            + "\n"
            + "  toList {\n"
            + "    var result = List.new()\n"
            + "    for (element in this) {\n"
            + "      result.add(element)\n"
            + "    }\n"
            + "    return result\n"
            + "  }\n"
            + "}\n"
            + "\n"
            + "class MapSequence is Sequence {\n"
            + "  construct new(sequence, fn) {\n"
            + "    _sequence = sequence\n"
            + "    _fn = fn\n"
            + "  }\n"
            + "\n"
            + "  iterate(iterator) { _sequence.iterate(iterator) }\n"
            + "  iteratorValue(iterator) { _fn.call(_sequence.iteratorValue(iterator)) }\n"
            + "}\n"
            + "\n"
            + "class WhereSequence is Sequence {\n"
            + "  construct new(sequence, fn) {\n"
            + "    _sequence = sequence\n"
            + "    _fn = fn\n"
            + "  }\n"
            + "\n"
            + "  iterate(iterator) {\n"
            + "    while (iterator = _sequence.iterate(iterator)) {\n"
            + "      if (_fn.call(_sequence.iteratorValue(iterator))) break\n"
            + "    }\n"
            + "    return iterator\n"
            + "  }\n"
            + "\n"
            + "  iteratorValue(iterator) { _sequence.iteratorValue(iterator) }\n"
            + "}\n"
            + "\n"
            + "class String is Sequence {\n"
            + "  bytes { StringByteSequence.new(this) }\n"
            + "  codePoints { StringCodePointSequence.new(this) }\n"
            + "}\n"
            + "\n"
            + "class StringByteSequence is Sequence {\n"
            + "  construct new(string) {\n"
            + "   _string = string\n"
            + "   }\n"
            + "\n"
            + "  [index] { _string.byteAt_(index) }\n"
            + "  iterate(iterator) { _string.iterateByte_(iterator) }\n"
            + "  iteratorValue(iterator) { _string.byteAt_(iterator) }\n"
            + "\n"
            + "  count { _string.byteCount_ }\n"
            + "}\n"
            + "\n"
            + "class StringCodePointSequence is Sequence {\n"
            + "  construct new(string) {\n"
            + "    _string = string\n"
            + "  }\n"
            + "\n"
            + "  [index] { _string.codePointAt_(index) }\n"
            + "  iterate(iterator) { _string.iterate(iterator) }\n"
            + "  iteratorValue(iterator) { _string.codePointAt_(iterator) }\n"
            + "\n"
            + "  count { _string.count }\n"
            + "}\n"
            + "\n"
            + "class List is Sequence {\n"
            + "  addAll(other) {\n"
            + "    for (element in other) {\n"
            + "      add(element)\n"
            + "    }\n"
            + "    return other\n"
            + "  }\n"
            + "\n"
            + "  toString { \"[\" + join(\", \") + \"]\" }\n"
            + "\n"
            + "  +(other) {\n"
            + "    var result = this[0..-1]\n"
            + "    for (element in other) {\n"
            + "      result.add(element)\n"
            + "    }\n"
            + "    return result\n"
            + "  }\n"
            + "}\n"
            + "\n"
            + "class Map {\n"
            + "  keys { MapKeySequence.new(this) }\n"
            + "  values { MapValueSequence.new(this) }\n"
            + "\n"
            + "  toString {\n"
            + "    var first = true\n"
            + "    var result = \"{\"\n"
            + "\n"
            + "    for (key in keys) {\n"
            + "      if (!first) result = result + \", \"\n"
            + "      first = false\n"
            + "      result = result + key.toString + \": \" + this[key].toString\n"
            + "    }\n"
            + "\n"
            + "    return result + \"}\"\n"
            + "  }\n"
            + "}\n"
            + "\n"
            + "class MapKeySequence is Sequence {\n"
            + "  construct new(map) {\n"
            + "    _map = map\n"
            + "  }\n"
            + "\n"
            + "  iterate(n) { _map.iterate_(n) }\n"
            + "  iteratorValue(iterator) { _map.keyIteratorValue_(iterator) }\n"
            + "}\n"
            + "\n"
            + "class MapValueSequence is Sequence {\n"
            + "  construct new(map) {\n"
            + "    _map = map\n"
            + "  }\n"
            + "\n"
            + "  iterate(n) { _map.iterate_(n) }\n"
            + "  iteratorValue(iterator) { _map.valueIteratorValue_(iterator) }\n"
            + "}\n"
            + "\n"
            + "class Range is Sequence {}\n"
            + "\n"
            + "class System {\n"
            + "  static print() {\n"
            + "    writeString_(\"\\n\")\n"
            + "  }\n"
            + "\n"
            + "  static print(obj) {\n"
            + "    writeObject_(obj)\n"
            + "    return obj\n"
            + "  }\n"
            + "\n"
            + "  static printAll(sequence) {\n"
            + "    for (object in sequence) writeObject_(object)\n"
            + "  }\n"
            + "\n"
            + "  static write(obj) {\n"
            + "    writeObject_(obj)\n"
            + "    return obj\n"
            + "  }\n"
            + "\n"
            + "  static writeAll(sequence) {\n"
            + "    for (object in sequence) writeObject_(object)\n"
            + "  }\n"
            + "\n"
            + "  static writeObject_(obj) {\n"
            + "    var string = obj.toString\n"
            + "    if (string is String) {\n"
            + "      writeString_(string)\n"
            + "    } else {\n"
            + "      writeString_(\"[invalid toString]\")\n"
            + "    }\n"
            + "  }\n"
            + "}\n";

        private static readonly Regex CheckDouble = new Regex(@"^(0[0-7]*|((?!0)|[-+]|(?=0+\.))(\d*\.)?\d+([eE]([-+])?\d+)?)$");

        static bool prim_bool_not(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.Bool(args[stackStart] != Obj.True);
            return true;
        }

        static bool prim_bool_toString(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = args[stackStart] == Obj.True ? ObjString.TrueString : ObjString.FalseString;
            return true;
        }

        static bool prim_class_name(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = ((ObjClass)args[stackStart]).Name;
            return true;
        }

        static bool prim_class_supertype(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjClass classObj = (ObjClass)args[stackStart];

            // Object has no superclass.
            args[stackStart] = classObj.Superclass ?? Obj.Null;
            return true;
        }

        static bool prim_fiber_new(WrenVM vm, Obj[] args, int stackStart)
        {
            Obj o = args[stackStart + 1];
            if (o is ObjFn || o is ObjClosure)
            {
                ObjFiber newFiber = new ObjFiber(o);

                // The compiler expects the first slot of a function to hold the receiver.
                // Since a fiber's stack is invoked directly, it doesn't have one, so put it
                // in here.
                newFiber.Push(Obj.Null);

                args[stackStart] = newFiber;
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Argument must be a function.");
            return false;
        }

        static bool prim_fiber_abort(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1] == Obj.Null)
                return true;
            vm.Fiber.Error = args[stackStart + 1];
            return false;
        }

        static bool prim_fiber_call(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjFiber runFiber = args[stackStart] as ObjFiber;

            if (runFiber != null)
            {
                if (runFiber.NumFrames != 0)
                {
                    if (runFiber.Caller == null)
                    {
                        // Remember who ran it.
                        runFiber.Caller = vm.Fiber;

                        // If the fiber was yielded, make the yield call return null.
                        if (runFiber.StackTop > 0)
                        {
                            runFiber.StoreValue(-1, Obj.Null);
                        }

                        return false;
                    }

                    vm.Fiber.Error = Obj.MakeString("Fiber has already been called.");
                    return false;
                }
                vm.Fiber.Error = Obj.MakeString("Cannot call a finished fiber.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Trying to call a non-fiber");
            return false;
        }

        static bool prim_fiber_call1(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjFiber runFiber = args[stackStart] as ObjFiber;

            if (runFiber != null)
            {
                if (runFiber.NumFrames != 0)
                {
                    if (runFiber.Caller == null)
                    {
                        // Remember who ran it.
                        runFiber.Caller = vm.Fiber;

                        // If the fiber was yielded, make the yield call return the value passed to
                        // run.
                        if (runFiber.StackTop > 0)
                        {
                            runFiber.StoreValue(-1, args[stackStart + 1]);
                        }

                        // When the calling fiber resumes, we'll store the result of the run call
                        // in its stack. Since fiber.run(value) has two arguments (the fiber and the
                        // value) and we only need one slot for the result, discard the other slot
                        // now.
                        vm.Fiber.StackTop--;

                        return false;
                    }

                    vm.Fiber.Error = Obj.MakeString("Fiber has already been called.");
                    return false;
                }
                vm.Fiber.Error = Obj.MakeString("Cannot call a finished fiber.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Trying to call a non-fiber");
            return false;
        }

        static bool prim_fiber_current(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = vm.Fiber;
            return true;
        }

        static bool prim_fiber_suspend(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.Null;
            return false;
        }

        static bool prim_fiber_error(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjFiber runFiber = (ObjFiber)args[stackStart];
            args[stackStart] = runFiber.Error ?? Obj.Null;
            return true;
        }

        static bool prim_fiber_isDone(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjFiber runFiber = (ObjFiber)args[stackStart];
            args[stackStart] = Obj.Bool(runFiber.NumFrames == 0 || runFiber.Error != null);
            return true;
        }

        static bool prim_fiber_transfer(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjFiber runFiber = (ObjFiber)args[stackStart];

            if (runFiber.NumFrames != 0)
            {
                if (runFiber.Caller == null && runFiber.StackTop > 0)
                {
                    runFiber.StoreValue(-1, Obj.Null);
                }

                // Unlike run, this does not remember the calling fiber. Instead, it
                // remember's *that* fiber's caller. You can think of it like tail call
                // elimination. The switched-from fiber is discarded and when the switched
                // to fiber completes or yields, control passes to the switched-from fiber's
                // caller.
                runFiber.Caller = vm.Fiber.Caller;

                return false;
            }

            // If the fiber was yielded, make the yield call return null.
            vm.Fiber.Error = Obj.MakeString("Cannot run a finished fiber.");
            return false;
        }

        static bool prim_fiber_transfer1(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjFiber runFiber = (ObjFiber)args[stackStart];

            if (runFiber.NumFrames != 0)
            {
                if (runFiber.Caller == null && runFiber.StackTop > 0)
                {
                    runFiber.StoreValue(-1, args[stackStart + 1]);
                }

                // Unlike run, this does not remember the calling fiber. Instead, it
                // remember's *that* fiber's caller. You can think of it like tail call
                // elimination. The switched-from fiber is discarded and when the switched
                // to fiber completes or yields, control passes to the switched-from fiber's
                // caller.
                runFiber.Caller = vm.Fiber.Caller;

                return false;
            }

            // If the fiber was yielded, make the yield call return the value passed to
            // run.
            vm.Fiber.Error = Obj.MakeString("Cannot run a finished fiber.");
            return false;
        }

        static bool prim_fiber_try(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjFiber runFiber = (ObjFiber)args[stackStart];

            if (runFiber.NumFrames != 0)
            {
                if (runFiber.Caller == null)
                {
                    runFiber.Caller = vm.Fiber;
                    runFiber.CallerIsTrying = true;

                    // If the fiber was yielded, make the yield call return null.
                    if (runFiber.StackTop > 0)
                    {
                        runFiber.StoreValue(-1, Obj.Null);
                    }

                    return false;
                }

                // Remember who ran it.
                vm.Fiber.Error = Obj.MakeString("Fiber has already been called.");
                return false;
            }
            vm.Fiber.Error = Obj.MakeString("Cannot try a finished fiber.");
            return false;
        }

        static bool prim_fiber_yield(WrenVM vm, Obj[] args, int stackStart)
        {
            // Unhook this fiber from the one that called it.
            ObjFiber caller = vm.Fiber.Caller;
            vm.Fiber.Caller = null;
            vm.Fiber.CallerIsTrying = false;

            // If we don't have any other pending fibers, jump all the way out of the
            // interpreter.
            if (caller == null)
            {
                args[stackStart] = Obj.Null;
            }
            else
            {
                // Make the caller's run method return null.
                caller.StoreValue(-1, Obj.Null);

                // Return the fiber to resume.
                args[stackStart] = caller;
            }

            return false;
        }

        static bool prim_fiber_yield1(WrenVM vm, Obj[] args, int stackStart)
        {
            // Unhook this fiber from the one that called it.
            ObjFiber caller = vm.Fiber.Caller;
            vm.Fiber.Caller = null;
            vm.Fiber.CallerIsTrying = false;

            // If we don't have any other pending fibers, jump all the way out of the
            // interpreter.
            if (caller == null)
            {
                args[stackStart] = Obj.Null;
            }
            else
            {
                // Make the caller's run method return the argument passed to yield.
                caller.StoreValue(-1, args[stackStart + 1]);

                // When the yielding fiber resumes, we'll store the result of the yield call
                // in its stack. Since Fiber.yield(value) has two arguments (the Fiber class
                // and the value) and we only need one slot for the result, discard the other
                // slot now.
                vm.Fiber.StackTop--;

                // Return the fiber to resume.
                args[stackStart] = caller;
            }

            return false;
        }

        static bool prim_fn_new(WrenVM vm, Obj[] args, int stackStart)
        {
            Obj v = args[stackStart + 1];
            if (v != null && (v is ObjFn || v is ObjClosure))
            {
                args[stackStart] = args[stackStart + 1];
                return true;
            }

            // The block argument is already a function, so just return it.
            vm.Fiber.Error = Obj.MakeString("Argument must be a function.");
            return false;
        }

        static bool prim_fn_arity(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjFn fn = args[stackStart] as ObjFn;
            args[stackStart] = fn != null ? new Obj(fn.Arity) : new Obj(0.0);
            return true;
        }

        static bool prim_fn_toString(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.MakeString("<fn>");
            return true;
        }

        static bool prim_foreign_toString(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.MakeString((args[stackStart] as ObjForeign).foreign.ToString());
            return true;
        }

        static bool prim_list_instantiate(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new ObjList(16);
            return true;
        }

        static bool prim_list_add(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjList list = args[stackStart] as ObjList;
            if (list == null)
            {
                vm.Fiber.Error = Obj.MakeString("Trying to add to a non-list");
                return false;
            }
            list.Add(args[stackStart + 1]);
            args[stackStart] = args[stackStart + 1];
            return true;
        }

        static bool prim_list_clear(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjList list = args[stackStart] as ObjList;
            if (list == null)
            {
                vm.Fiber.Error = Obj.MakeString("Trying to clear a non-list");
                return false;
            }
            list.Clear();

            args[stackStart] = Obj.Null;
            return true;
        }

        static bool prim_list_count(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjList list = args[stackStart] as ObjList;
            if (list != null)
            {
                args[stackStart] = new Obj(list.Count());
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Trying to clear a non-list");
            return false;
        }

        static bool prim_list_insert(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjList list = (ObjList)args[stackStart];

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;
                if (args[stackStart + 1].Num == index)
                {
                    if (index < 0)
                        index += list.Count() + 1;
                    if (index >= 0 && index <= list.Count())
                    {
                        list.Insert(args[stackStart + 2], index);
                        args[stackStart] = args[stackStart + 2];
                        return true;
                    }
                    vm.Fiber.Error = Obj.MakeString("Index out of bounds.");
                    return false;
                }

                // count + 1 here so you can "insert" at the very end.
                vm.Fiber.Error = Obj.MakeString("Index must be an integer.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Index must be a number.");
            return false;
        }

        static bool prim_list_iterate(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjList list = (ObjList)args[stackStart];

            // If we're starting the iteration, return the first index.
            if (args[stackStart + 1] == Obj.Null)
            {
                if (list.Count() != 0)
                {
                    args[stackStart] = new Obj(0.0);
                    return true;
                }

                args[stackStart] = Obj.False;
                return true;
            }

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;
                if (args[stackStart + 1].Num == index)
                {
                    if (!(index < 0) && !(index >= list.Count() - 1))
                    {
                        // Move to the next index.
                        args[stackStart] = new Obj(index + 1);
                        return true;
                    }

                    // Stop if we're out of bounds.
                    args[stackStart] = Obj.False;
                    return true;
                }

                vm.Fiber.Error = Obj.MakeString("Iterator must be an integer.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Iterator must be a number.");
            return false;
        }

        static bool prim_list_iteratorValue(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjList list = (ObjList)args[stackStart];

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;
                if (args[stackStart + 1].Num == index)
                {
                    if (index >= 0 && index < list.Count())
                    {
                        args[stackStart] = list.Get(index);
                        return true;
                    }

                    vm.Fiber.Error = Obj.MakeString("Iterator out of bounds.");
                    return false;
                }

                vm.Fiber.Error = Obj.MakeString("Iterator must be an integer.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Iterator must be a number.");
            return false;
        }

        static bool prim_list_removeAt(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjList list = (ObjList)args[stackStart];

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;
                if (args[stackStart + 1].Num == index)
                {
                    if (index < 0)
                        index += list.Count();
                    if (index >= 0 && index < list.Count())
                    {
                        args[stackStart] = list.RemoveAt(index);
                        return true;
                    }

                    vm.Fiber.Error = Obj.MakeString("Index out of bounds.");
                    return false;
                }

                vm.Fiber.Error = Obj.MakeString("Index must be an integer.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Index must be a number.");
            return false;
        }

        static bool prim_list_subscript(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjList list = (ObjList)args[stackStart];

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;
                if (index == args[stackStart + 1].Num)
                {
                    if (index < 0)
                    {
                        index += list.Count();
                    }
                    if (index >= 0 && index < list.Count())
                    {
                        args[stackStart] = list.Get(index);
                        return true;
                    }

                    vm.Fiber.Error = Obj.MakeString("Subscript out of bounds.");
                    return false;
                }
                vm.Fiber.Error = Obj.MakeString("Subscript must be an integer.");
                return false;
            }

            ObjRange r = args[stackStart + 1] as ObjRange;

            if (r == null)
            {
                vm.Fiber.Error = Obj.MakeString("Subscript must be a number or a range.");
                return false;
            }

            // TODO: This is seriously broken and needs a rewrite
            int from = (int)r.From;
            if (from != r.From)
            {
                vm.Fiber.Error = Obj.MakeString("Range start must be an integer.");
                return false;
            }
            int to = (int)r.To;
            if (to != r.To)
            {
                vm.Fiber.Error = Obj.MakeString("Range end must be an integer.");
                return false;
            }

            if (from < 0)
                from += list.Count();
            if (to < 0)
                to += list.Count();

            int step = to < from ? -1 : 1;

            if (step > 0 && r.IsInclusive)
                to += 1;
            if (step < 0 && !r.IsInclusive)
                to += 1;

            // Handle copying an empty list
            if (list.Count() == 0 && to == (r.IsInclusive ? -1 : 0))
            {
                to = 0;
                step = 1;
            }

            int count = (to - from) * step + (step < 0 ? 1 : 0);

            if (to < 0 || from + (count * step) > list.Count())
            {
                vm.Fiber.Error = Obj.MakeString("Range end out of bounds.");
                return false;
            }
            if (from < 0 || (from >= list.Count() && from > 0))
            {
                vm.Fiber.Error = Obj.MakeString("Range start out of bounds.");
                return false;
            }

            ObjList result = new ObjList(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(list.Get(from + (i * step)));
            }

            args[stackStart] = result;
            return true;
        }

        static bool prim_list_subscriptSetter(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjList list = (ObjList)args[stackStart];
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;

                if (index == args[stackStart + 1].Num)
                {
                    if (index < 0)
                    {
                        index += list.Count();
                    }

                    if (list != null && index >= 0 && index < list.Count())
                    {
                        list.Set(args[stackStart + 2], index);
                        args[stackStart] = args[stackStart + 2];
                        return true;
                    }

                    vm.Fiber.Error = Obj.MakeString("Subscript out of bounds.");
                    return false;
                }

                vm.Fiber.Error = Obj.MakeString("Subscript must be an integer.");
                return false;
            }
            vm.Fiber.Error = Obj.MakeString("Subscript must be a number.");
            return false;
        }

        static bool prim_map_instantiate(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new ObjMap();
            return true;
        }

        static bool prim_map_subscript(WrenVM vm, Obj[] args, int stackStart)
        {
            Obj a = args[stackStart + 1];
            if (ValidateKey(a))
            {
                ObjMap map = args[stackStart] as ObjMap;
                if (map != null)
                {
                    args[stackStart] = map.Get(a);
                    if (args[stackStart] == Obj.Undefined)
                    {
                        args[stackStart] = Obj.Null;
                    }
                }
                else
                {
                    args[stackStart] = Obj.Null;
                }
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Key must be a value type or fiber.");
            return false;
        }

        static bool prim_map_subscriptSetter(WrenVM vm, Obj[] args, int stackStart)
        {
            if (ValidateKey(args[stackStart + 1]))
            {
                ObjMap map = args[stackStart] as ObjMap;

                if (map != null)
                {
                    map.Set(args[stackStart + 1], args[stackStart + 2]);
                }
                args[stackStart] = args[stackStart + 2];
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Key must be a value type or fiber.");
            return false;
        }

        static bool prim_map_clear(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjMap m = args[stackStart] as ObjMap;
            if (m != null)
                m.Clear();
            args[stackStart] = Obj.Null;
            return true;
        }

        static bool prim_map_containsKey(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjMap map = (ObjMap)args[stackStart];

            if (ValidateKey(args[stackStart + 1]))
            {
                Obj v = map.Get(args[stackStart + 1]);

                args[stackStart] = Obj.Bool(v != Obj.Undefined);
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Key must be a value type or fiber.");
            return false;
        }

        static bool prim_map_count(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjMap m = (ObjMap)args[stackStart];
            args[stackStart] = new Obj(m.Count());
            return true;
        }

        private static bool prim_map_iterate(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjMap map = (ObjMap)args[stackStart];

            if (map.Count() == 0)
            {
                args[stackStart] = Obj.False;
                return true;
            }

            // Start one past the last entry we stopped at.
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                if (args[stackStart + 1].Num < 0)
                {
                    args[stackStart] = Obj.False;
                    return true;
                }
                int index = (int)args[stackStart + 1].Num;

                if (index == args[stackStart + 1].Num)
                {
                    args[stackStart] = index > map.Count() || map.Get(index) == Obj.Undefined ? Obj.False : new Obj(index + 1);
                    return true;
                }

                vm.Fiber.Error = Obj.MakeString("Iterator must be an integer.");
                return false;
            }

            // If we're starting the iteration, start at the first used entry.
            if (args[stackStart + 1] == Obj.Null)
            {
                args[stackStart] = new Obj(1);
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Iterator must be a number.");
            return false;
        }

        static bool prim_map_remove(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjMap map = (ObjMap)args[stackStart];

            if (ValidateKey(args[stackStart + 1]))
            {
                args[stackStart] = map != null ? map.Remove(args[stackStart + 1]) : Obj.Null;
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Key must be a value type or fiber.");
            return false;
        }

        static bool prim_map_keyIteratorValue(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjMap map = (ObjMap)args[stackStart];

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;

                if (index == args[stackStart + 1].Num)
                {
                    if (map != null && index >= 0)
                    {
                        args[stackStart] = map.GetKey(index - 1);
                        return true;
                    }
                    vm.Fiber.Error = Obj.MakeString("Error in prim_map_keyIteratorValue.");
                    return false;
                }

                vm.Fiber.Error = Obj.MakeString("Iterator must be an integer.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Iterator must be a number.");
            return false;
        }

        static bool prim_map_valueIteratorValue(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjMap map = (ObjMap)args[stackStart];

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;

                if (index == args[stackStart + 1].Num)
                {
                    if (map != null && index >= 0 && index < map.Count())
                    {
                        args[stackStart] = map.Get(index - 1);
                        return true;
                    }
                    vm.Fiber.Error = Obj.MakeString("Error in prim_map_valueIteratorValue.");
                    return false;
                }

                vm.Fiber.Error = Obj.MakeString("Iterator must be an integer.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Iterator must be a number.");
            return false;
        }

        static bool prim_null_not(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.True;
            return true;
        }

        static bool prim_null_toString(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.MakeString("null");
            return true;
        }

        static bool prim_num_fromString(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString s = args[stackStart + 1] as ObjString;

            if (s != null)
            {
                if (s.Str.Length != 0)
                {
                    double n;

                    if (double.TryParse(s.Str, out n))
                    {
                        args[stackStart] = new Obj(n);
                        return true;
                    }

                    if (CheckDouble.IsMatch(s.Str))
                    {
                        vm.Fiber.Error = Obj.MakeString("Number literal is too large.");
                        return false;
                    }
                }

                args[stackStart] = Obj.Null;
                return true;
            }

            // Corner case: Can't parse an empty string.
            vm.Fiber.Error = Obj.MakeString("Argument must be a string.");
            return false;
        }

        static bool prim_num_pi(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.PI);
            return true;
        }

        static bool prim_num_minus(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj(args[stackStart].Num - args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_plus(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj(args[stackStart].Num + args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_multiply(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj(args[stackStart].Num * args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_divide(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj(args[stackStart].Num / args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_lt(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = Obj.Bool(args[stackStart].Num < args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_gt(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = Obj.Bool(args[stackStart].Num > args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_lte(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = Obj.Bool(args[stackStart].Num <= args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_gte(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = Obj.Bool(args[stackStart].Num >= args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_And(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj((Int64)args[stackStart].Num & (Int64)args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }
        static bool prim_num_Or(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj((Int64)args[stackStart].Num | (Int64)args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_Xor(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj((Int64)args[stackStart].Num ^ (Int64)args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }
        static bool prim_num_LeftShift(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj((Int64)args[stackStart].Num << (int)args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }
        static bool prim_num_RightShift(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj((Int64)args[stackStart].Num >> (int)args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_abs(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Abs(args[stackStart].Num));
            return true;
        }
        static bool prim_num_acos(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Acos(args[stackStart].Num));
            return true;
        }
        static bool prim_num_asin(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Asin(args[stackStart].Num));
            return true;
        }
        static bool prim_num_atan(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Atan(args[stackStart].Num));
            return true;
        }
        static bool prim_num_ceil(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Ceiling(args[stackStart].Num));
            return true;
        }
        static bool prim_num_cos(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Cos(args[stackStart].Num));
            return true;
        }
        static bool prim_num_floor(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Floor(args[stackStart].Num));
            return true;
        }
        static bool prim_num_negate(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(-args[stackStart].Num);
            return true;
        }
        static bool prim_num_sin(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Sin(args[stackStart].Num));
            return true;
        }
        static bool prim_num_sqrt(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Sqrt(args[stackStart].Num));
            return true;
        }
        static bool prim_num_tan(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Tan(args[stackStart].Num));
            return true;
        }

        static bool prim_num_mod(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = new Obj(args[stackStart].Num % args[stackStart + 1].Num);
                return true;
            }
            vm.Fiber.Error = Obj.MakeString("Right operand must be a number.");
            return false;
        }

        static bool prim_num_eqeq(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = Obj.Bool(args[stackStart].Num == args[stackStart + 1].Num);
                return true;
            }

            args[stackStart] = Obj.False;
            return true;
        }

        static bool prim_num_bangeq(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                args[stackStart] = Obj.Bool(args[stackStart].Num != args[stackStart + 1].Num);
                return true;
            }

            args[stackStart] = Obj.True;
            return true;
        }

        static bool prim_num_bitwiseNot(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(~(Int64)args[stackStart].Num);
            // Bitwise operators always work on 64-bit signed ints.
            return true;
        }

        static bool prim_num_dotDot(WrenVM vm, Obj[] args, int stackStart)
        {
            return range_from_numbers(vm, args[stackStart], args[stackStart + 1], true, out args[stackStart]);
        }

        static bool prim_num_dotDotDot(WrenVM vm, Obj[] args, int stackStart)
        {
            return range_from_numbers(vm, args[stackStart], args[stackStart + 1], false, out args[stackStart]);
        }

        static bool range_from_numbers(WrenVM vm, Obj start, Obj end, bool inclusive, out Obj range)
        {
            if (end.Type == ObjType.Num)
            {
                double from = start.Num;
                double to = end.Num;
                range = new ObjRange(from, to, inclusive);
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Right hand side of range must be a number.");
            range = null;
            return false;
        }

        static bool prim_num_atan2(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Atan2(args[stackStart].Num, args[stackStart + 1].Num));
            return true;
        }

        static bool prim_num_fraction(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(args[stackStart].Num - Math.Truncate(args[stackStart].Num));
            return true;
        }

        static bool prim_num_isNan(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.Bool(double.IsNaN(args[stackStart].Num));
            return true;
        }

        static bool prim_num_isInfinity(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.Bool(double.IsInfinity(args[stackStart].Num));
            return true;
        }

        static bool prim_num_isInteger(WrenVM vm, Obj[] args, int stackStart)
        {
            double v = args[stackStart].Num;
            args[stackStart] = Obj.Bool(!double.IsNaN(v) && !double.IsInfinity(v) && v == Math.Truncate(v));
            return true;
        }

        static bool prim_num_sign(WrenVM vm, Obj[] args, int stackStart)
        {
            double value = args[stackStart].Num;
            args[stackStart] = new Obj(Math.Sign(value));
            return true;
        }

        static bool prim_num_toString(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.MakeString(args[stackStart].Num.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        static bool prim_num_truncate(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(Math.Truncate(args[stackStart].Num));
            return true;
        }

        static bool prim_object_same(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.Bool(Obj.Equals(args[stackStart + 1], args[stackStart + 2]));
            return true;
        }

        static bool prim_object_not(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.False;
            return true;
        }

        static bool prim_object_eqeq(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.Bool(Obj.Equals(args[stackStart], args[stackStart + 1]));
            return true;
        }

        static bool prim_object_bangeq(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.Bool(!Obj.Equals(args[stackStart], args[stackStart + 1]));
            return true;
        }

        static bool prim_object_is(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1] as ObjClass != null)
            {
                ObjClass classObj = args[stackStart].GetClass();
                ObjClass baseClassObj = args[stackStart + 1] as ObjClass;

                // Walk the superclass chain looking for the class.
                do
                {
                    if (baseClassObj == classObj)
                    {
                        args[stackStart] = Obj.True;
                        return true;
                    }

                    classObj = classObj.Superclass;
                } while (classObj != null);

                args[stackStart] = Obj.False;
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Right operand must be a class.");
            return false;
        }

        static bool prim_object_toString(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjClass cClass = args[stackStart] as ObjClass;
            ObjInstance instance = args[stackStart] as ObjInstance;
            if (cClass != null)
            {
                args[stackStart] = cClass.Name;
            }
            else if (instance != null)
            {
                ObjString name = instance.ClassObj.Name;
                args[stackStart] = Obj.MakeString(string.Format("instance of {0}", name));
            }
            else if (args[stackStart] == Obj.Undefined)
            {
                args[stackStart] = Obj.MakeString("undefined");
            }
            else if (args[stackStart] == Obj.Null)
            {
                args[stackStart] = Obj.MakeString("null");
            }
            else
            {
                args[stackStart] = Obj.MakeString("<object>");
            }
            return true;
        }

        static bool prim_object_type(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = args[stackStart].GetClass();
            return true;
        }

        static bool prim_range_from(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(((ObjRange)args[stackStart]).From);
            return true;
        }

        static bool prim_range_to(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(((ObjRange)args[stackStart]).To);
            return true;
        }

        static bool prim_range_min(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjRange range = (ObjRange)args[stackStart];
            args[stackStart] = range.From < range.To ? new Obj(range.From) : new Obj(range.To);
            return true;
        }

        static bool prim_range_max(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjRange range = (ObjRange)args[stackStart];
            args[stackStart] = range.From > range.To ? new Obj(range.From) : new Obj(range.To);
            return true;
        }

        static bool prim_range_isInclusive(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = Obj.Bool(((ObjRange)args[stackStart]).IsInclusive);
            return true;
        }

        static bool prim_range_iterate(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjRange range = (ObjRange)args[stackStart];

            // Special case: empty range.
            if (range.From == range.To && !range.IsInclusive)
            {
                args[stackStart] = Obj.False;
                return true;
            }

            // Start the iteration.
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                double iterator = args[stackStart + 1].Num;

                // Iterate towards [to] from [from].
                if (range.From < range.To)
                {
                    iterator++;
                    if (iterator > range.To)
                    {
                        args[stackStart] = Obj.False;
                        return true;
                    }
                }
                else
                {
                    iterator--;
                    if (iterator < range.To)
                    {
                        args[stackStart] = Obj.False;
                        return true;
                    }
                }

                if (!range.IsInclusive && iterator == range.To)
                {
                    args[stackStart] = Obj.False;
                    return true;
                }

                args[stackStart] = new Obj(iterator);
                return true;
            }
            if (args[stackStart + 1] == Obj.Null)
            {
                args[stackStart] = new Obj(range.From);
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Iterator must be a number.");
            return false;
        }

        static bool prim_range_iteratorValue(WrenVM vm, Obj[] args, int stackStart)
        {
            // Assume the iterator is a number so that is the value of the range.
            args[stackStart] = args[stackStart + 1];
            return true;
        }

        static bool prim_range_toString(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjRange range = args[stackStart] as ObjRange;

            if (range != null)
                args[stackStart] = Obj.MakeString(string.Format("{0}{1}{2}", range.From, range.IsInclusive ? ".." : "...", range.To));
            return true;
        }

        static bool prim_string_eqeq(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString aString = (ObjString)args[stackStart];
            ObjString bString = args[stackStart + 1] as ObjString;
            args[stackStart] = Obj.Bool(aString != null && bString != null && aString.Str == bString.Str);
            return true;
        }

        static bool prim_string_bangeq(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString aString = (ObjString)args[stackStart];
            ObjString bString = args[stackStart + 1] as ObjString;
            args[stackStart] = Obj.Bool(aString == null || bString == null || aString.Str != bString.Str);
            return true;
        }

        static bool prim_string_fromCodePoint(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int codePoint = (int)args[stackStart + 1].Num;

                if (codePoint == args[stackStart + 1].Num)
                {
                    if (codePoint >= 0)
                    {
                        if (codePoint <= 0x10ffff)
                        {
                            args[stackStart] = ObjString.FromCodePoint(codePoint);
                            return true;
                        }

                        vm.Fiber.Error = Obj.MakeString("Code point cannot be greater than 0x10ffff.");
                        return false;
                    }
                    vm.Fiber.Error = Obj.MakeString("Code point cannot be negative.");
                    return false;
                }

                vm.Fiber.Error = Obj.MakeString("Code point must be an integer.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Code point must be a number.");
            return false;
        }

        static bool prim_string_byteAt(WrenVM vm, Obj[] args, int stackStart)
        {
            Byte[] s = ((ObjString)args[stackStart]).GetBytes();

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;

                if (index == args[stackStart + 1].Num)
                {
                    if (index >= 0 && index < s.Length)
                    {
                        args[stackStart] = new Obj(s[index]);
                        return true;
                    }

                    vm.Fiber.Error = Obj.MakeString("Index out of bounds.");
                    return false;
                }

                vm.Fiber.Error = Obj.MakeString("Index must be an integer.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Index must be a number.");
            return false;
        }

        private static bool prim_string_byteCount(WrenVM vm, Obj[] args, int stackStart)
        {
            Byte[] s = ((ObjString)args[stackStart]).GetBytes();
            args[stackStart] = new Obj(s.Length);
            return true;
        }

        static bool prim_string_codePointAt(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString s = args[stackStart] as ObjString;

            if (s == null)
            {
                return false;
            }

            if (args[stackStart + 1].Type != ObjType.Num)
            {
                vm.Fiber.Error = Obj.MakeString("Index must be a number.");
                return false;
            }

            int index = (int)args[stackStart + 1].Num;

            if (index != args[stackStart + 1].Num)
            {
                vm.Fiber.Error = Obj.MakeString("Index must be an integer.");
                return false;
            }

            if (index < 0 || index >= s.Str.Length)
            {
                vm.Fiber.Error = Obj.MakeString("Index out of bounds.");
                return false;
            }

            args[stackStart] = new Obj(s.Str[index]);
            return true;
        }

        static bool prim_string_contains(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString s = (ObjString)args[stackStart];
            ObjString search = args[stackStart + 1] as ObjString;

            if (search == null)
            {
                vm.Fiber.Error = Obj.MakeString("Argument must be a string.");
                return false;
            }

            args[stackStart] = Obj.Bool(s.Str.Contains(search.Str));
            return true;
        }

        static bool prim_string_count(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj(args[stackStart].ToString().Length);
            return true;
        }

        static bool prim_string_endsWith(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString s = (ObjString)args[stackStart];
            ObjString search = args[stackStart + 1] as ObjString;

            if (search == null)
            {
                vm.Fiber.Error = Obj.MakeString("Argument must be a string.");
                return false;
            }

            args[stackStart] = Obj.Bool(s.Str.EndsWith(search.Str));
            return true;
        }

        static bool prim_string_indexOf(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString s = (ObjString)args[stackStart];
            ObjString search = args[stackStart + 1] as ObjString;

            if (search != null)
            {
                int index = s.Str.IndexOf(search.Str, StringComparison.Ordinal);
                args[stackStart] = new Obj(index);
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Argument must be a string.");
            return false;
        }

        static bool prim_string_iterate(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString s = (ObjString)args[stackStart];

            // If we're starting the iteration, return the first index.
            if (args[stackStart + 1] == Obj.Null)
            {
                if (s.Str.Length != 0)
                {
                    args[stackStart] = new Obj(0.0);
                    return true;
                }
                args[stackStart] = Obj.False;
                return true;
            }

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                if (args[stackStart + 1].Num < 0)
                {
                    args[stackStart] = Obj.False;
                    return true;
                }
                int index = (int)args[stackStart + 1].Num;

                if (index == args[stackStart + 1].Num)
                {
                    index++;
                    if (index >= s.Str.Length)
                    {
                        args[stackStart] = Obj.False;
                        return true;
                    }

                    args[stackStart] = new Obj(index);
                    return true;
                }

                // Advance to the beginning of the next UTF-8 sequence.
                vm.Fiber.Error = Obj.MakeString("Iterator must be an integer.");
                return false;
            }

            vm.Fiber.Error = Obj.MakeString("Iterator must be a number.");
            return false;
        }

        static bool prim_string_iterateByte(WrenVM vm, Obj[] args, int stackStart)
        {
            Byte[] s = ((ObjString)args[stackStart]).GetBytes();

            // If we're starting the iteration, return the first index.
            if (args[stackStart + 1] == Obj.Null)
            {
                if (s.Length == 0)
                {
                    args[stackStart] = Obj.False;
                    return true;
                }
                args[stackStart] = new Obj(0.0);
                return true;
            }

            if (args[stackStart + 1].Type != ObjType.Num)
            {
                vm.Fiber.Error = Obj.MakeString("dunno what to put in here.");
                return false;
            }

            if (args[stackStart + 1].Num < 0)
            {
                args[stackStart] = Obj.False;
                return true;
            }
            int index = (int)args[stackStart + 1].Num;

            // Advance to the next byte.
            index++;
            if (index >= s.Length)
            {
                args[stackStart] = Obj.False;
                return true;
            }

            args[stackStart] = new Obj(index);
            return true;
        }

        static bool prim_string_iteratorValue(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString s = (ObjString)args[stackStart];

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;

                if (index == args[stackStart + 1].Num)
                {
                    if (index < s.Str.Length && index >= 0)
                    {
                        args[stackStart] = Obj.MakeString("" + s.Str[index]);
                        return true;
                    }

                    vm.Fiber.Error = Obj.MakeString("Iterator out of bounds.");
                    return false;
                }

                vm.Fiber.Error = Obj.MakeString("Iterator must be an integer.");
                return false;
            }
            vm.Fiber.Error = Obj.MakeString("Iterator must be a number.");
            return false;
        }

        static bool prim_string_startsWith(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString s = (ObjString)args[stackStart];
            ObjString search = args[stackStart + 1] as ObjString;

            if (search != null)
            {
                args[stackStart] = Obj.Bool(s.Str.StartsWith(search.Str));
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Argument must be a string.");
            return false;
        }

        static bool prim_string_toString(WrenVM vm, Obj[] args, int stackStart)
        {
            return true;
        }

        static bool prim_string_plus(WrenVM vm, Obj[] args, int stackStart)
        {
            ObjString s1 = args[stackStart + 1] as ObjString;
            if (s1 != null)
            {
                args[stackStart] = Obj.MakeString(((ObjString)args[stackStart]).Str + s1.Str);
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Right operand must be a string.");
            return false;
        }

        static bool prim_string_subscript(WrenVM vm, Obj[] args, int stackStart)
        {
            string s = ((ObjString)args[stackStart]).Str;

            if (args[stackStart + 1].Type == ObjType.Num)
            {
                int index = (int)args[stackStart + 1].Num;

                if (index == args[stackStart + 1].Num)
                {
                    if (index < 0)
                    {
                        index += s.Length;
                    }

                    if (index >= 0 && index < s.Length)
                    {
                        args[stackStart] = ObjString.FromCodePoint(s[index]);
                        return true;
                    }

                    vm.Fiber.Error = Obj.MakeString("Subscript out of bounds.");
                    return false;
                }

                vm.Fiber.Error = Obj.MakeString("Subscript must be an integer.");
                return false;
            }

            if (args[stackStart + 1] as ObjRange != null)
            {
                ObjRange r = (ObjRange)args[stackStart + 1];

                // TODO: This is seriously broken and needs a rewrite
                int from = (int)r.From;
                if (from != r.From)
                {
                    vm.Fiber.Error = Obj.MakeString("Range start must be an integer.");
                    return false;
                }
                int to = (int)r.To;
                if (to != r.To)
                {
                    vm.Fiber.Error = Obj.MakeString("Range end must be an integer.");
                    return false;
                }

                if (from < 0)
                    from += s.Length;
                if (to < 0)
                    to += s.Length;

                int step = to < from ? -1 : 1;

                if (step > 0 && r.IsInclusive)
                    to += 1;
                if (step < 0 && !r.IsInclusive)
                    to += 1;

                // Handle copying an empty string
                if (s.Length == 0 && to == (r.IsInclusive ? -1 : 0))
                {
                    to = 0;
                    step = 1;
                }

                int count = (to - from) * step + (step < 0 ? 1 : 0);

                if (to < 0 || from + (count * step) > s.Length)
                {
                    vm.Fiber.Error = Obj.MakeString("Range end out of bounds.");
                    return false;
                }
                if (from < 0 || (from >= s.Length && from > 0))
                {
                    vm.Fiber.Error = Obj.MakeString("Range start out of bounds.");
                    return false;
                }

                string result = "";
                for (int i = 0; i < count; i++)
                {
                    result += s[from + (i * step)];
                }

                args[stackStart] = Obj.MakeString(result);
                return true;
            }

            vm.Fiber.Error = Obj.MakeString("Subscript must be a number or a range.");
            return false;
        }

        static bool WriteString(WrenVM vm, Obj[] args, int stackStart)
        {
            if (args[stackStart + 1] != null && args[stackStart + 1].Type == ObjType.Obj)
            {
                string s = args[stackStart + 1].ToString();
                vm.Write(s);
            }
            args[stackStart] = Obj.Null;
            return true;
        }

        static bool Clock(WrenVM vm, Obj[] args, int stackStart)
        {
            args[stackStart] = new Obj((double)DateTime.Now.Ticks / 10000000);
            return true;
        }

        // Creates either the Object or Class class in the core library with [name].
        static ObjClass DefineClass(WrenVM vm, string name)
        {
            ObjString nameString = new ObjString(name);

            ObjClass classObj = new ObjClass(0, nameString);

            vm.DefineVariable(null, name, classObj);

            return classObj;
        }

        static bool ValidateKey(Obj arg)
        {
            return arg == Obj.False   || arg == Obj.True
                   || arg == Obj.Null || arg.Type == ObjType.Num
                   || arg is ObjClass || arg is ObjFiber
                   || arg is ObjRange || arg is ObjString;
        }

        public CoreLibrary(WrenVM v)
        {
            _vm = v;
        }

        public void InitializeCore()
        {
            // Define the root Object class. This has to be done a little specially
            // because it has no superclass.
            WrenVM.ObjectClass = DefineClass(_vm, "Object");
            _vm.Primitive(WrenVM.ObjectClass, "!", prim_object_not);
            _vm.Primitive(WrenVM.ObjectClass, "==(_)", prim_object_eqeq);
            _vm.Primitive(WrenVM.ObjectClass, "!=(_)", prim_object_bangeq);
            _vm.Primitive(WrenVM.ObjectClass, "is(_)", prim_object_is);
            _vm.Primitive(WrenVM.ObjectClass, "toString", prim_object_toString);
            _vm.Primitive(WrenVM.ObjectClass, "type", prim_object_type);

            // Now we can define Class, which is a subclass of Object.
            WrenVM.ClassClass = DefineClass(_vm, "Class");
            WrenVM.ClassClass.BindSuperclass(WrenVM.ObjectClass);
            // Store a copy of the class in ObjClass
            ObjClass.ClassClass = WrenVM.ClassClass;
            // Define the primitives
            _vm.Primitive(WrenVM.ClassClass, "name", prim_class_name);
            _vm.Primitive(WrenVM.ClassClass, "supertype", prim_class_supertype);

            // Finally, we can define Object's metaclass which is a subclass of Class.
            ObjClass objectMetaclass = DefineClass(_vm, "Object metaclass");

            // Wire up the metaclass relationships now that all three classes are built.
            WrenVM.ObjectClass.ClassObj = objectMetaclass;
            objectMetaclass.ClassObj = WrenVM.ClassClass;
            WrenVM.ClassClass.ClassObj = WrenVM.ClassClass;

            // Do this after wiring up the metaclasses so objectMetaclass doesn't get
            // collected.
            objectMetaclass.BindSuperclass(WrenVM.ClassClass);

            _vm.Primitive(objectMetaclass, "same(_,_)", prim_object_same);

            // The core class diagram ends up looking like this, where single lines point
            // to a class's superclass, and double lines point to its metaclass:
            //
            //        .------------------------------------. .====.
            //        |                  .---------------. | #    #
            //        v                  |               v | v    #
            //   .---------.   .-------------------.   .-------.  #
            //   | Object  |==>| Object metaclass  |==>| Class |=="
            //   '---------'   '-------------------'   '-------'
            //        ^                                 ^ ^ ^ ^
            //        |                  .--------------' # | #
            //        |                  |                # | #
            //   .---------.   .-------------------.      # | # -.
            //   |  Base   |==>|  Base metaclass   |======" | #  |
            //   '---------'   '-------------------'        | #  |
            //        ^                                     | #  |
            //        |                  .------------------' #  | Example classes
            //        |                  |                    #  |
            //   .---------.   .-------------------.          #  |
            //   | Derived |==>| Derived metaclass |=========="  |
            //   '---------'   '-------------------'            -'

            // The rest of the classes can now be defined normally.
            _vm.Interpret("", "", CoreLibSource);

            WrenVM.BoolClass = (ObjClass)_vm.FindVariable("Bool");
            _vm.Primitive(WrenVM.BoolClass, "toString", prim_bool_toString);
            _vm.Primitive(WrenVM.BoolClass, "!", prim_bool_not);

            WrenVM.FiberClass = (ObjClass)_vm.FindVariable("Fiber");
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "new(_)", prim_fiber_new);
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "abort(_)", prim_fiber_abort);
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "current", prim_fiber_current);
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "suspend()", prim_fiber_suspend);
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "yield()", prim_fiber_yield);
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "yield(_)", prim_fiber_yield1);
            _vm.Primitive(WrenVM.FiberClass, "call()", prim_fiber_call);
            _vm.Primitive(WrenVM.FiberClass, "call(_)", prim_fiber_call1);
            _vm.Primitive(WrenVM.FiberClass, "error", prim_fiber_error);
            _vm.Primitive(WrenVM.FiberClass, "isDone", prim_fiber_isDone);
            _vm.Primitive(WrenVM.FiberClass, "transfer()", prim_fiber_transfer);
            _vm.Primitive(WrenVM.FiberClass, "transfer(_)", prim_fiber_transfer1);
            _vm.Primitive(WrenVM.FiberClass, "try()", prim_fiber_try);

            WrenVM.FnClass = (ObjClass)_vm.FindVariable("Fn");
            _vm.Primitive(WrenVM.FnClass.ClassObj, "new(_)", prim_fn_new);

            _vm.Primitive(WrenVM.FnClass, "arity", prim_fn_arity);

            _vm.Call(WrenVM.FnClass, "call()");
            _vm.Call(WrenVM.FnClass, "call(_)");
            _vm.Call(WrenVM.FnClass, "call(_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_,_,_)");
            _vm.Call(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_)");

            _vm.Primitive(WrenVM.FnClass, "toString", prim_fn_toString);

            WrenVM.ForeignClass = (ObjClass)_vm.FindVariable("Foreign");
            _vm.Primitive(WrenVM.ForeignClass, "toString", prim_foreign_toString);

            WrenVM.NullClass = (ObjClass)_vm.FindVariable("Null");
            _vm.Primitive(WrenVM.NullClass, "!", prim_null_not);
            _vm.Primitive(WrenVM.NullClass, "toString", prim_null_toString);

            WrenVM.NumClass = (ObjClass)_vm.FindVariable("Num");
            _vm.Primitive(WrenVM.NumClass.ClassObj, "fromString(_)", prim_num_fromString);
            _vm.Primitive(WrenVM.NumClass.ClassObj, "pi", prim_num_pi);
            _vm.Primitive(WrenVM.NumClass, "-(_)", prim_num_minus);
            _vm.Primitive(WrenVM.NumClass, "+(_)", prim_num_plus);
            _vm.Primitive(WrenVM.NumClass, "*(_)", prim_num_multiply);
            _vm.Primitive(WrenVM.NumClass, "/(_)", prim_num_divide);
            _vm.Primitive(WrenVM.NumClass, "<(_)", prim_num_lt);
            _vm.Primitive(WrenVM.NumClass, ">(_)", prim_num_gt);
            _vm.Primitive(WrenVM.NumClass, "<=(_)", prim_num_lte);
            _vm.Primitive(WrenVM.NumClass, ">=(_)", prim_num_gte);
            _vm.Primitive(WrenVM.NumClass, "&(_)", prim_num_And);
            _vm.Primitive(WrenVM.NumClass, "|(_)", prim_num_Or);
            _vm.Primitive(WrenVM.NumClass, "^(_)", prim_num_Xor);
            _vm.Primitive(WrenVM.NumClass, "<<(_)", prim_num_LeftShift);
            _vm.Primitive(WrenVM.NumClass, ">>(_)", prim_num_RightShift);
            _vm.Primitive(WrenVM.NumClass, "abs", prim_num_abs);
            _vm.Primitive(WrenVM.NumClass, "acos", prim_num_acos);
            _vm.Primitive(WrenVM.NumClass, "asin", prim_num_asin);
            _vm.Primitive(WrenVM.NumClass, "atan", prim_num_atan);
            _vm.Primitive(WrenVM.NumClass, "ceil", prim_num_ceil);
            _vm.Primitive(WrenVM.NumClass, "cos", prim_num_cos);
            _vm.Primitive(WrenVM.NumClass, "floor", prim_num_floor);
            _vm.Primitive(WrenVM.NumClass, "-", prim_num_negate);
            _vm.Primitive(WrenVM.NumClass, "sin", prim_num_sin);
            _vm.Primitive(WrenVM.NumClass, "sqrt", prim_num_sqrt);
            _vm.Primitive(WrenVM.NumClass, "tan", prim_num_tan);
            _vm.Primitive(WrenVM.NumClass, "%(_)", prim_num_mod);
            _vm.Primitive(WrenVM.NumClass, "~", prim_num_bitwiseNot);
            _vm.Primitive(WrenVM.NumClass, "..(_)", prim_num_dotDot);
            _vm.Primitive(WrenVM.NumClass, "...(_)", prim_num_dotDotDot);
            _vm.Primitive(WrenVM.NumClass, "atan(_)", prim_num_atan2);
            _vm.Primitive(WrenVM.NumClass, "fraction", prim_num_fraction);
            _vm.Primitive(WrenVM.NumClass, "isNan", prim_num_isNan);
            _vm.Primitive(WrenVM.NumClass, "isInfinity", prim_num_isInfinity);
            _vm.Primitive(WrenVM.NumClass, "isInteger", prim_num_isInteger);
            _vm.Primitive(WrenVM.NumClass, "sign", prim_num_sign);
            _vm.Primitive(WrenVM.NumClass, "toString", prim_num_toString);
            _vm.Primitive(WrenVM.NumClass, "truncate", prim_num_truncate);

            // These are defined just so that 0 and -0 are equal, which is specified by
            // IEEE 754 even though they have different bit representations.
            _vm.Primitive(WrenVM.NumClass, "==(_)", prim_num_eqeq);
            _vm.Primitive(WrenVM.NumClass, "!=(_)", prim_num_bangeq);

            WrenVM.StringClass = (ObjClass)_vm.FindVariable("String");
            _vm.Primitive(WrenVM.StringClass.ClassObj, "fromCodePoint(_)", prim_string_fromCodePoint);
            _vm.Primitive(WrenVM.StringClass, "==(_)", prim_string_eqeq);
            _vm.Primitive(WrenVM.StringClass, "!=(_)", prim_string_bangeq);
            _vm.Primitive(WrenVM.StringClass, "+(_)", prim_string_plus);
            _vm.Primitive(WrenVM.StringClass, "[_]", prim_string_subscript);
            _vm.Primitive(WrenVM.StringClass, "byteAt_(_)", prim_string_byteAt);
            _vm.Primitive(WrenVM.StringClass, "byteCount_", prim_string_byteCount);
            _vm.Primitive(WrenVM.StringClass, "codePointAt_(_)", prim_string_codePointAt);
            _vm.Primitive(WrenVM.StringClass, "contains(_)", prim_string_contains);
            _vm.Primitive(WrenVM.StringClass, "count", prim_string_count);
            _vm.Primitive(WrenVM.StringClass, "endsWith(_)", prim_string_endsWith);
            _vm.Primitive(WrenVM.StringClass, "indexOf(_)", prim_string_indexOf);
            _vm.Primitive(WrenVM.StringClass, "iterate(_)", prim_string_iterate);
            _vm.Primitive(WrenVM.StringClass, "iterateByte_(_)", prim_string_iterateByte);
            _vm.Primitive(WrenVM.StringClass, "iteratorValue(_)", prim_string_iteratorValue);
            _vm.Primitive(WrenVM.StringClass, "startsWith(_)", prim_string_startsWith);
            _vm.Primitive(WrenVM.StringClass, "toString", prim_string_toString);

            WrenVM.ListClass = (ObjClass)_vm.FindVariable("List");
            _vm.Primitive(WrenVM.ListClass.ClassObj, "new()", prim_list_instantiate);
            _vm.Primitive(WrenVM.ListClass, "[_]", prim_list_subscript);
            _vm.Primitive(WrenVM.ListClass, "[_]=(_)", prim_list_subscriptSetter);
            _vm.Primitive(WrenVM.ListClass, "add(_)", prim_list_add);
            _vm.Primitive(WrenVM.ListClass, "clear()", prim_list_clear);
            _vm.Primitive(WrenVM.ListClass, "count", prim_list_count);
            _vm.Primitive(WrenVM.ListClass, "insert(_,_)", prim_list_insert);
            _vm.Primitive(WrenVM.ListClass, "iterate(_)", prim_list_iterate);
            _vm.Primitive(WrenVM.ListClass, "iteratorValue(_)", prim_list_iteratorValue);
            _vm.Primitive(WrenVM.ListClass, "removeAt(_)", prim_list_removeAt);

            WrenVM.MapClass = (ObjClass)_vm.FindVariable("Map");
            _vm.Primitive(WrenVM.MapClass.ClassObj, "new()", prim_map_instantiate);
            _vm.Primitive(WrenVM.MapClass, "[_]", prim_map_subscript);
            _vm.Primitive(WrenVM.MapClass, "[_]=(_)", prim_map_subscriptSetter);
            _vm.Primitive(WrenVM.MapClass, "clear()", prim_map_clear);
            _vm.Primitive(WrenVM.MapClass, "containsKey(_)", prim_map_containsKey);
            _vm.Primitive(WrenVM.MapClass, "count", prim_map_count);
            _vm.Primitive(WrenVM.MapClass, "remove(_)", prim_map_remove);
            _vm.Primitive(WrenVM.MapClass, "iterate_(_)", prim_map_iterate);
            _vm.Primitive(WrenVM.MapClass, "keyIteratorValue_(_)", prim_map_keyIteratorValue);
            _vm.Primitive(WrenVM.MapClass, "valueIteratorValue_(_)", prim_map_valueIteratorValue);

            WrenVM.RangeClass = (ObjClass)_vm.FindVariable("Range");
            _vm.Primitive(WrenVM.RangeClass, "from", prim_range_from);
            _vm.Primitive(WrenVM.RangeClass, "to", prim_range_to);
            _vm.Primitive(WrenVM.RangeClass, "min", prim_range_min);
            _vm.Primitive(WrenVM.RangeClass, "max", prim_range_max);
            _vm.Primitive(WrenVM.RangeClass, "isInclusive", prim_range_isInclusive);
            _vm.Primitive(WrenVM.RangeClass, "iterate(_)", prim_range_iterate);
            _vm.Primitive(WrenVM.RangeClass, "iteratorValue(_)", prim_range_iteratorValue);
            _vm.Primitive(WrenVM.RangeClass, "toString", prim_range_toString);

            ObjClass system = (ObjClass)_vm.FindVariable("System");
            _vm.Primitive(system.ClassObj, "writeString_(_)", WriteString);
            _vm.Primitive(system.ClassObj, "clock", Clock);

            system.ClassObj.IsSealed = true;

            // While bootstrapping the core types and running the core library, a number
            // of string objects have been created, many of which were instantiated
            // before stringClass was stored in the VM. Some of them *must* be created
            // first -- the ObjClass for string itself has a reference to the ObjString
            // for its name.
            //
            // These all currently have a NULL classObj pointer, so go back and assign
            // them now that the string class is known.
            ObjString.InitClass();

            WrenVM.ClassClass.IsSealed = true;
            WrenVM.FiberClass.IsSealed = true;
            WrenVM.FnClass.IsSealed = true;
            WrenVM.ListClass.IsSealed = true;
            WrenVM.MapClass.IsSealed = true;
            WrenVM.RangeClass.IsSealed = true;
            WrenVM.StringClass.IsSealed = true;
        }
    }
}
