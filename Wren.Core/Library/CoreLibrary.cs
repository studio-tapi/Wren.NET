﻿using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Wren.Core.Objects;
using Wren.Core.VM;
using ValueType = Wren.Core.VM.ValueType;

namespace Wren.Core.Library
{
    class CoreLibrary
    {
        private readonly WrenVM _vm;

        private const string CoreLibSource =
            "class Bool {}\n"
            + "class Fiber {}\n"
            + "class Fn {}\n"
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
            + "    writeString_(\"\\n\")\n"
            + "    return obj\n"
            + "  }\n"
            + "\n"
            + "  static printAll(sequence) {\n"
            + "    for (object in sequence) writeObject_(object)\n"
            + "    writeString_(\"\\n\")\n"
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

        static PrimitiveResult prim_bool_not(WrenVM vm, Value[] args)
        {
            args[0] = new Value(args[0].Type != ValueType.True);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_bool_toString(WrenVM vm, Value[] args)
        {
            if (args[0].Type == ValueType.True)
            {
                args[0] = new Value("true");
            }
            else
            {
                args[0] = new Value("false");
            }
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_class_name(WrenVM vm, Value[] args)
        {
            args[0] = new Value(((ObjClass)args[0].Obj).Name);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_class_supertype(WrenVM vm, Value[] args)
        {
            ObjClass classObj = (ObjClass)args[0].Obj;

            // Object has no superclass.
            if (classObj.Superclass == null)
            {
                args[0] = new Value(ValueType.Null);
            }
            else
            {
                args[0] = new Value(classObj.Superclass);
            }
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fiber_new(WrenVM vm, Value[] args)
        {
            Obj o = args[1].Obj;
            if (o is ObjFn || o is ObjClosure)
            {
                ObjFiber newFiber = new ObjFiber(o);

                // The compiler expects the first slot of a function to hold the receiver.
                // Since a fiber's stack is invoked directly, it doesn't have one, so put it
                // in here.
                newFiber.Push(Value.Null);

                args[0] = new Value(newFiber);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Argument must be a function.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_abort(WrenVM vm, Value[] args)
        {
            Obj o = args[1].Obj;
            args[0] = o is ObjString ? args[1] : new Value("Error message must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_call(WrenVM vm, Value[] args)
        {
            ObjFiber runFiber = args[0].Obj as ObjFiber;

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
                            runFiber.StoreValue(-1, new Value(ValueType.Null));
                        }

                        return PrimitiveResult.RunFiber;
                    }

                    args[0] = new Value("Fiber has already been called.");
                    return PrimitiveResult.Error;
                }
                args[0] = new Value("Cannot call a finished fiber.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Trying to call a non-fiber");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_call1(WrenVM vm, Value[] args)
        {
            ObjFiber runFiber = args[0].Obj as ObjFiber;

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
                            runFiber.StoreValue(-1, args[1]);
                        }

                        // When the calling fiber resumes, we'll store the result of the run call
                        // in its stack. Since fiber.run(value) has two arguments (the fiber and the
                        // value) and we only need one slot for the result, discard the other slot
                        // now.
                        vm.Fiber.StackTop--;

                        return PrimitiveResult.RunFiber;
                    }

                    args[0] = new Value("Fiber has already been called.");
                    return PrimitiveResult.Error;
                }
                args[0] = new Value("Cannot call a finished fiber.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Trying to call a non-fiber");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_current(WrenVM vm, Value[] args)
        {
            args[0] = new Value(vm.Fiber);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fiber_error(WrenVM vm, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;
            args[0] = runFiber.Error == null ? new Value(ValueType.Null) : new Value(runFiber.Error);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fiber_isDone(WrenVM vm, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;
            args[0] = new Value(runFiber.NumFrames == 0 || runFiber.Error != null);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_fiber_run(WrenVM vm, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;

            if (runFiber.NumFrames != 0)
            {
                if (runFiber.Caller == null && runFiber.StackTop > 0)
                {
                    runFiber.StoreValue(-1, new Value(ValueType.Null));
                }

                // Unlike run, this does not remember the calling fiber. Instead, it
                // remember's *that* fiber's caller. You can think of it like tail call
                // elimination. The switched-from fiber is discarded and when the switched
                // to fiber completes or yields, control passes to the switched-from fiber's
                // caller.
                runFiber.Caller = vm.Fiber.Caller;

                return PrimitiveResult.RunFiber;
            }

            // If the fiber was yielded, make the yield call return null.
            args[0] = new Value("Cannot run a finished fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_run1(WrenVM vm, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;

            if (runFiber.NumFrames != 0)
            {
                if (runFiber.Caller == null && runFiber.StackTop > 0)
                {
                    runFiber.StoreValue(-1, args[1]);
                }

                // Unlike run, this does not remember the calling fiber. Instead, it
                // remember's *that* fiber's caller. You can think of it like tail call
                // elimination. The switched-from fiber is discarded and when the switched
                // to fiber completes or yields, control passes to the switched-from fiber's
                // caller.
                runFiber.Caller = vm.Fiber.Caller;

                return PrimitiveResult.RunFiber;
            }

            // If the fiber was yielded, make the yield call return the value passed to
            // run.
            args[0] = new Value("Cannot run a finished fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_try(WrenVM vm, Value[] args)
        {
            ObjFiber runFiber = (ObjFiber)args[0].Obj;

            if (runFiber.NumFrames != 0)
            {
                if (runFiber.Caller == null)
                {
                    runFiber.Caller = vm.Fiber;
                    runFiber.CallerIsTrying = true;

                    // If the fiber was yielded, make the yield call return null.
                    if (runFiber.StackTop > 0)
                    {
                        runFiber.StoreValue(-1, new Value(ValueType.Null));
                    }

                    return PrimitiveResult.RunFiber;
                }

                // Remember who ran it.
                args[0] = new Value("Fiber has already been called.");
                return PrimitiveResult.Error;
            }
            args[0] = new Value("Cannot try a finished fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fiber_yield(WrenVM vm, Value[] args)
        {
            // Unhook this fiber from the one that called it.
            ObjFiber caller = vm.Fiber.Caller;
            vm.Fiber.Caller = null;
            vm.Fiber.CallerIsTrying = false;

            // If we don't have any other pending fibers, jump all the way out of the
            // interpreter.
            if (caller == null)
            {
                args[0] = new Value(ValueType.Null);
            }
            else
            {
                // Make the caller's run method return null.
                caller.StoreValue(-1, new Value(ValueType.Null));

                // Return the fiber to resume.
                args[0] = new Value(caller);
            }

            return PrimitiveResult.RunFiber;
        }

        static PrimitiveResult prim_fiber_yield1(WrenVM vm, Value[] args)
        {
            // Unhook this fiber from the one that called it.
            ObjFiber caller = vm.Fiber.Caller;
            vm.Fiber.Caller = null;
            vm.Fiber.CallerIsTrying = false;

            // If we don't have any other pending fibers, jump all the way out of the
            // interpreter.
            if (caller == null)
            {
                args[0] = new Value(ValueType.Null);
            }
            else
            {
                // Make the caller's run method return the argument passed to yield.
                caller.StoreValue(-1, args[1]);

                // When the yielding fiber resumes, we'll store the result of the yield call
                // in its stack. Since Fiber.yield(value) has two arguments (the Fiber class
                // and the value) and we only need one slot for the result, discard the other
                // slot now.
                vm.Fiber.StackTop--;

                // Return the fiber to resume.
                args[0] = new Value(caller);
            }

            return PrimitiveResult.RunFiber;
        }

        static PrimitiveResult prim_fn_new(WrenVM vm, Value[] args)
        {
            Obj v = args[1].Obj;
            if (v != null && (v is ObjFn || v is ObjClosure))
            {
                args[0] = args[1];
                return PrimitiveResult.Value;
            }

            // The block argument is already a function, so just return it.
            args[0] = new Value("Argument must be a function.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fn_arity(WrenVM vm, Value[] args)
        {
            ObjFn fn = args[0].Obj as ObjFn;
            args[0] = fn != null ? new Value(fn.Arity) : new Value(0.0);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult CallFn(Value[] args, int numArgs)
        {
            ObjFn fn = args[0].Obj as ObjFn;
            ObjClosure c = args[0].Obj as ObjClosure;
            if (c != null)
            {
                fn = c.Function;
            }

            if (fn != null)
            {
                if (numArgs >= fn.Arity)
                {
                    return PrimitiveResult.Call;
                }

                args[0] = new Value("Function expects more arguments.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Object should be a function or closure");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_fn_call0(WrenVM vm, Value[] args) { return CallFn(args, 0); }
        static PrimitiveResult prim_fn_call1(WrenVM vm, Value[] args) { return CallFn(args, 1); }
        static PrimitiveResult prim_fn_call2(WrenVM vm, Value[] args) { return CallFn(args, 2); }
        static PrimitiveResult prim_fn_call3(WrenVM vm, Value[] args) { return CallFn(args, 3); }
        static PrimitiveResult prim_fn_call4(WrenVM vm, Value[] args) { return CallFn(args, 4); }
        static PrimitiveResult prim_fn_call5(WrenVM vm, Value[] args) { return CallFn(args, 5); }
        static PrimitiveResult prim_fn_call6(WrenVM vm, Value[] args) { return CallFn(args, 6); }
        static PrimitiveResult prim_fn_call7(WrenVM vm, Value[] args) { return CallFn(args, 7); }
        static PrimitiveResult prim_fn_call8(WrenVM vm, Value[] args) { return CallFn(args, 8); }
        static PrimitiveResult prim_fn_call9(WrenVM vm, Value[] args) { return CallFn(args, 9); }
        static PrimitiveResult prim_fn_call10(WrenVM vm, Value[] args) { return CallFn(args, 10); }
        static PrimitiveResult prim_fn_call11(WrenVM vm, Value[] args) { return CallFn(args, 11); }
        static PrimitiveResult prim_fn_call12(WrenVM vm, Value[] args) { return CallFn(args, 12); }
        static PrimitiveResult prim_fn_call13(WrenVM vm, Value[] args) { return CallFn(args, 13); }
        static PrimitiveResult prim_fn_call14(WrenVM vm, Value[] args) { return CallFn(args, 14); }
        static PrimitiveResult prim_fn_call15(WrenVM vm, Value[] args) { return CallFn(args, 15); }
        static PrimitiveResult prim_fn_call16(WrenVM vm, Value[] args) { return CallFn(args, 16); }

        static PrimitiveResult prim_fn_toString(WrenVM vm, Value[] args)
        {
            args[0] = new Value("<fn>");
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_instantiate(WrenVM vm, Value[] args)
        {
            args[0] = new Value(new ObjList(16));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_add(WrenVM vm, Value[] args)
        {
            ObjList list = args[0].Obj as ObjList;
            if (list == null)
            {
                args[0] = new Value("Trying to add to a non-list");
                return PrimitiveResult.Error;
            }
            list.Add(args[1]);
            args[0] = args[1];
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_clear(WrenVM vm, Value[] args)
        {
            ObjList list = args[0].Obj as ObjList;
            if (list == null)
            {
                args[0] = new Value("Trying to clear a non-list");
                return PrimitiveResult.Error;
            }
            list.Clear();

            args[0] = new Value(ValueType.Null);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_count(WrenVM vm, Value[] args)
        {
            ObjList list = args[0].Obj as ObjList;
            if (list != null)
            {
                args[0] = new Value(list.Count());
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Trying to clear a non-list");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_insert(WrenVM vm, Value[] args)
        {
            ObjList list = (ObjList)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;
                if (args[1].Num == index)
                {
                    if (index < 0)
                        index += list.Count() + 1;
                    if (index >= 0 && index <= list.Count())
                    {
                        list.Insert(args[2], index);
                        args[0] = args[2];
                        return PrimitiveResult.Value;
                    }
                    args[0] = new Value("Index out of bounds.");
                    return PrimitiveResult.Error;
                }

                // count + 1 here so you can "insert" at the very end.
                args[0] = new Value("Index must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Index must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_iterate(WrenVM vm, Value[] args)
        {
            ObjList list = (ObjList)args[0].Obj;

            // If we're starting the iteration, return the first index.
            if (args[1].Type == ValueType.Null)
            {
                if (list.Count() != 0)
                {
                    args[0] = new Value(0.0);
                    return PrimitiveResult.Value;
                }

                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;
                if (args[1].Num == index)
                {
                    if (!(index < 0) && !(index >= list.Count() - 1))
                    {
                        // Move to the next index.
                        args[0] = new Value(index + 1);
                        return PrimitiveResult.Value;
                    }

                    // Stop if we're out of bounds.
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_iteratorValue(WrenVM vm, Value[] args)
        {
            ObjList list = (ObjList)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;
                if (args[1].Num == index)
                {
                    if (index >= 0 && index < list.Count())
                    {
                        args[0] = list.Get(index);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Iterator out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_removeAt(WrenVM vm, Value[] args)
        {
            ObjList list = (ObjList)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;
                if (args[1].Num == index)
                {
                    if (index < 0)
                        index += list.Count();
                    if (index >= 0 && index < list.Count())
                    {
                        args[0] = list.RemoveAt(index);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Index out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Index must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Index must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_list_subscript(WrenVM vm, Value[] args)
        {
            ObjList list = (ObjList)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;
                if (index == args[1].Num)
                {
                    if (index < 0)
                    {
                        index += list.Count();
                    }
                    if (index >= 0 && index < list.Count())
                    {
                        args[0] = list.Get(index);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Subscript out of bounds.");
                    return PrimitiveResult.Error;
                }
                args[0] = new Value("Subscript must be an integer.");
                return PrimitiveResult.Error;
            }

            ObjRange r = args[1].Obj as ObjRange;

            if (r == null)
            {
                args[0] = new Value("Subscript must be a number or a range.");
                return PrimitiveResult.Error;
            }

            // TODO: This is seriously broken and needs a rewrite
            int from = (int)r.From;
            if (from != r.From)
            {
                args[0] = new Value("Range start must be an integer.");
                return PrimitiveResult.Error;
            }
            int to = (int)r.To;
            if (to != r.To)
            {
                args[0] = new Value("Range end must be an integer.");
                return PrimitiveResult.Error;
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
                args[0] = new Value("Range end out of bounds.");
                return PrimitiveResult.Error;
            }
            if (from < 0 || (from >= list.Count() && from > 0))
            {
                args[0] = new Value("Range start out of bounds.");
                return PrimitiveResult.Error;
            }

            ObjList result = new ObjList(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(list.Get(from + (i * step)));
            }

            args[0] = new Value(result);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_list_subscriptSetter(WrenVM vm, Value[] args)
        {
            ObjList list = (ObjList)args[0].Obj;
            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (index < 0)
                    {
                        index += list.Count();
                    }

                    if (list != null && index >= 0 && index < list.Count())
                    {
                        list.Set(args[2], index);
                        args[0] = args[2];
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Subscript out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Subscript must be an integer.");
                return PrimitiveResult.Error;
            }
            args[0] = new Value("Subscript must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_instantiate(WrenVM vm, Value[] args)
        {
            args[0] = new Value(new ObjMap());
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_map_subscript(WrenVM vm, Value[] args)
        {
            ObjMap map = args[0].Obj as ObjMap;

            if (ValidateKey(args[1]))
            {
                if (map != null)
                {
                    args[0] = map.Get(args[1]);
                    if (args[0].Type == ValueType.Undefined)
                    {
                        args[0] = new Value(ValueType.Null);
                    }
                }
                else
                {
                    args[0] = new Value(ValueType.Null);
                }
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Key must be a value type or fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_subscriptSetter(WrenVM vm, Value[] args)
        {
            ObjMap map = args[0].Obj as ObjMap;

            if (ValidateKey(args[1]))
            {
                if (map != null)
                {
                    map.Set(args[1], args[2]);
                }
                args[0] = args[2];
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Key must be a value type or fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_clear(WrenVM vm, Value[] args)
        {
            ObjMap m = args[0].Obj as ObjMap;
            if (m != null)
                m.Clear();
            args[0] = new Value(ValueType.Null);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_map_containsKey(WrenVM vm, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (ValidateKey(args[1]))
            {
                Value v = map.Get(args[1]);

                args[0] = new Value(v.Type != ValueType.Undefined);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Key must be a value type or fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_count(WrenVM vm, Value[] args)
        {
            ObjMap m = (ObjMap)args[0].Obj;
            args[0] = new Value(m.Count());
            return PrimitiveResult.Value;
        }

        private static PrimitiveResult prim_map_iterate(WrenVM vm, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (map.Count() == 0)
            {
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            // Start one past the last entry we stopped at.
            if (args[1].Type == ValueType.Num)
            {
                if (args[1].Num < 0)
                {
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    args[0] = index > map.Count() || map.Get(index).Type == ValueType.Undefined ? new Value(false) : new Value(index + 1);
                    return PrimitiveResult.Value;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            // If we're starting the iteration, start at the first used entry.
            if (args[1].Type == ValueType.Null)
            {
                args[0] = new Value(1);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_remove(WrenVM vm, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (ValidateKey(args[1]))
            {
                args[0] = map != null ? map.Remove(args[1]) : new Value(ValueType.Null);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Key must be a value type or fiber.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_keyIteratorValue(WrenVM vm, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (map != null && index >= 0)
                    {
                        args[0] = map.GetKey(index - 1);
                        return PrimitiveResult.Value;
                    }
                    args[0] = new Value("Error in prim_map_keyIteratorValue.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_map_valueIteratorValue(WrenVM vm, Value[] args)
        {
            ObjMap map = (ObjMap)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (map != null && index >= 0 && index < map.Count())
                    {
                        args[0] = map.Get(index - 1);
                        return PrimitiveResult.Value;
                    }
                    args[0] = new Value("Error in prim_map_valueIteratorValue.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_null_not(WrenVM vm, Value[] args)
        {
            args[0] = new Value(true);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_null_toString(WrenVM vm, Value[] args)
        {
            args[0] = new Value("null");
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_fromString(WrenVM vm, Value[] args)
        {
            ObjString s = args[1].Obj as ObjString;

            if (s != null)
            {
                if (s.Value.Length != 0)
                {
                    double n;

                    if (double.TryParse(s.Value, out n))
                    {
                        args[0] = new Value(n);
                        return PrimitiveResult.Value;
                    }

                    if (CheckDouble.IsMatch(s.Value))
                    {
                        args[0] = new Value("Number literal is too large.");
                        return PrimitiveResult.Error;
                    }
                }

                args[0] = new Value(ValueType.Null);
                return PrimitiveResult.Value;
            }

            // Corner case: Can't parse an empty string.
            args[0] = new Value("Argument must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_pi(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.PI);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_minus(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num - args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_plus(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num + args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_multiply(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num * args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_divide(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num / args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_lt(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num < args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_gt(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num > args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_lte(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num <= args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_gte(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num >= args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_And(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num & (Int64)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }
        static PrimitiveResult prim_num_Or(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num | (Int64)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_Xor(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num ^ (Int64)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }
        static PrimitiveResult prim_num_LeftShift(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num << (int)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }
        static PrimitiveResult prim_num_RightShift(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value((Int64)args[0].Num >> (int)args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_abs(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Abs(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_acos(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Acos(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_asin(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Asin(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_atan(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Atan(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_ceil(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Ceiling(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_cos(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Cos(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_floor(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Floor(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_negate(WrenVM vm, Value[] args)
        {
            args[0] = new Value(-args[0].Num);
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_sin(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Sin(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_sqrt(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Sqrt(args[0].Num));
            return PrimitiveResult.Value;
        }
        static PrimitiveResult prim_num_tan(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Tan(args[0].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_mod(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num % args[1].Num);
                return PrimitiveResult.Value;
            }
            args[0] = new Value("Right operand must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_eqeq(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num == args[1].Num);
                return PrimitiveResult.Value;
            }

            args[0] = new Value(false);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_bangeq(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                args[0] = new Value(args[0].Num != args[1].Num);
                return PrimitiveResult.Value;
            }

            args[0] = new Value(true);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_bitwiseNot(WrenVM vm, Value[] args)
        {
            args[0] = new Value(~(Int64)args[0].Num);
            // Bitwise operators always work on 64-bit signed ints.
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_dotDot(WrenVM vm, Value[] args)
        {
            return range_from_numbers(args[0], args[1], true, out args[0]);
        }

        static PrimitiveResult prim_num_dotDotDot(WrenVM vm, Value[] args)
        {
            return range_from_numbers(args[0], args[1], false, out args[0]);
        }

        static PrimitiveResult range_from_numbers(Value start, Value end, bool inclusive, out Value range)
        {
            if (end.Type == ValueType.Num)
            {
                double from = start.Num;
                double to = end.Num;
                range = new Value(new ObjRange(from, to, inclusive));
                return PrimitiveResult.Value;
            }

            range = new Value("Right hand side of range must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_num_atan2(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Atan2(args[0].Num, args[1].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_fraction(WrenVM vm, Value[] args)
        {
            args[0] = new Value(args[0].Num - Math.Truncate(args[0].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_isNan(WrenVM vm, Value[] args)
        {
            args[0] = new Value(double.IsNaN(args[0].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_isInfinity(WrenVM vm, Value[] args)
        {
            args[0] = new Value(double.IsInfinity(args[0].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_isInteger(WrenVM vm, Value[] args)
        {
            double v = args[0].Num;
            args[0] = new Value(!double.IsNaN(v) && !double.IsInfinity(v) && v == Math.Truncate(v));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_sign(WrenVM vm, Value[] args)
        {
            double value = args[0].Num;
            args[0] = new Value(Math.Sign(value));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_toString(WrenVM vm, Value[] args)
        {
            args[0] = new Value(args[0].Num.ToString(CultureInfo.InvariantCulture));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_num_truncate(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Math.Truncate(args[0].Num));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_same(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Value.Equals(args[1], args[2]));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_not(WrenVM vm, Value[] args)
        {
            args[0] = new Value(false);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_eqeq(WrenVM vm, Value[] args)
        {
            args[0] = new Value(Value.Equals(args[0], args[1]));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_bangeq(WrenVM vm, Value[] args)
        {
            args[0] = new Value(!Value.Equals(args[0], args[1]));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_is(WrenVM vm, Value[] args)
        {
            if (args[1].Obj as ObjClass != null)
            {
                ObjClass classObj = args[0].GetClass();
                ObjClass baseClassObj = args[1].Obj as ObjClass;

                // Walk the superclass chain looking for the class.
                do
                {
                    if (baseClassObj == classObj)
                    {
                        args[0] = new Value(true);
                        return PrimitiveResult.Value;
                    }

                    classObj = classObj.Superclass;
                } while (classObj != null);

                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Right operand must be a class.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_object_toString(WrenVM vm, Value[] args)
        {
            ObjClass cClass = args[0].Obj as ObjClass;
            ObjInstance instance = args[0].Obj as ObjInstance;
            if (cClass != null)
            {
                args[0] = new Value(cClass.Name);
            }
            else if (instance != null)
            {
                ObjString name = instance.ClassObj.Name;
                args[0] = new Value(string.Format("instance of {0}", name));
            }
            else
            {
                args[0] = new Value("<object>");
            }
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_object_type(WrenVM vm, Value[] args)
        {
            args[0] = new Value(args[0].GetClass());
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_from(WrenVM vm, Value[] args)
        {
            args[0] = new Value(((ObjRange)args[0].Obj).From);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_to(WrenVM vm, Value[] args)
        {
            args[0] = new Value(((ObjRange)args[0].Obj).To);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_min(WrenVM vm, Value[] args)
        {
            ObjRange range = (ObjRange)args[0].Obj;
            args[0] = range.From < range.To ? new Value(range.From) : new Value(range.To);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_max(WrenVM vm, Value[] args)
        {
            ObjRange range = (ObjRange)args[0].Obj;
            args[0] = range.From > range.To ? new Value(range.From) : new Value(range.To);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_isInclusive(WrenVM vm, Value[] args)
        {
            args[0] = new Value(((ObjRange)args[0].Obj).IsInclusive);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_iterate(WrenVM vm, Value[] args)
        {
            ObjRange range = (ObjRange)args[0].Obj;

            // Special case: empty range.
            if (range.From == range.To && !range.IsInclusive)
            {
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            // Start the iteration.
            if (args[1].Type == ValueType.Num)
            {
                double iterator = args[1].Num;

                // Iterate towards [to] from [from].
                if (range.From < range.To)
                {
                    iterator++;
                    if (iterator > range.To)
                    {
                        args[0] = new Value(false);
                        return PrimitiveResult.Value;
                    }
                }
                else
                {
                    iterator--;
                    if (iterator < range.To)
                    {
                        args[0] = new Value(false);
                        return PrimitiveResult.Value;
                    }
                }

                if (!range.IsInclusive && iterator == range.To)
                {
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }

                args[0] = new Value(iterator);
                return PrimitiveResult.Value;
            }
            if (args[1].Type == ValueType.Null)
            {
                args[0] = new Value(range.From);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_range_iteratorValue(WrenVM vm, Value[] args)
        {
            // Assume the iterator is a number so that is the value of the range.
            args[0] = args[1];
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_range_toString(WrenVM vm, Value[] args)
        {
            ObjRange range = args[0].Obj as ObjRange;

            if (range != null)
                args[0] = new Value(string.Format("{0}{1}{2}", range.From, range.IsInclusive ? ".." : "...", range.To));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_eqeq(WrenVM vm, Value[] args)
        {
            ObjString aString = (ObjString)args[0].Obj;
            ObjString bString = args[1].Obj as ObjString;
            args[0] = new Value(aString != null && bString != null && aString.Value == bString.Value);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_bangeq(WrenVM vm, Value[] args)
        {
            ObjString aString = (ObjString)args[0].Obj;
            ObjString bString = args[1].Obj as ObjString;
            args[0] = new Value(aString == null || bString == null || aString.Value != bString.Value);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_fromCodePoint(WrenVM vm, Value[] args)
        {
            if (args[1].Type == ValueType.Num)
            {
                int codePoint = (int)args[1].Num;

                if (codePoint == args[1].Num)
                {
                    if (codePoint >= 0)
                    {
                        if (codePoint <= 0x10ffff)
                        {
                            args[0] = ObjString.FromCodePoint(codePoint);
                            return PrimitiveResult.Value;
                        }

                        args[0] = new Value("Code point cannot be greater than 0x10ffff.");
                        return PrimitiveResult.Error;
                    }
                    args[0] = new Value("Code point cannot be negative.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Code point must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Code point must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_byteAt(WrenVM vm, Value[] args)
        {
            Byte[] s = ((ObjString)args[0].Obj).GetBytes();

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (index >= 0 && index < s.Length)
                    {
                        args[0] = new Value(s[index]);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Index out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Index must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Index must be a number.");
            return PrimitiveResult.Error;
        }

        private static PrimitiveResult prim_string_byteCount(WrenVM vm, Value[] args)
        {
            Byte[] s = ((ObjString)args[0].Obj).GetBytes();
            args[0] = new Value(s.Length);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_codePointAt(WrenVM vm, Value[] args)
        {
            ObjString s = args[0].Obj as ObjString;

            if (s == null)
            {
                return PrimitiveResult.Error;
            }

            if (args[1].Type != ValueType.Num)
            {
                args[0] = new Value("Index must be a number.");
                return PrimitiveResult.Error;
            }

            int index = (int)args[1].Num;

            if (index != args[1].Num)
            {
                args[0] = new Value("Index must be an integer.");
                return PrimitiveResult.Error;
            }

            if (index < 0 || index >= s.Value.Length)
            {
                args[0] = new Value("Index out of bounds.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value(s.Value[index]);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_contains(WrenVM vm, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;
            ObjString search = args[1].Obj as ObjString;

            if (search == null)
            {
                args[0] = new Value("Argument must be a string.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value(s.Value.Contains(search.Value));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_count(WrenVM vm, Value[] args)
        {
            args[0] = new Value(args[0].Obj.ToString().Length);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_endsWith(WrenVM vm, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;
            ObjString search = args[1].Obj as ObjString;

            if (search == null)
            {
                args[0] = new Value("Argument must be a string.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value(s.Value.EndsWith(search.Value));
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_indexOf(WrenVM vm, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;
            ObjString search = args[1].Obj as ObjString;

            if (search != null)
            {
                int index = s.Value.IndexOf(search.Value, StringComparison.Ordinal);
                args[0] = new Value(index);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Argument must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_iterate(WrenVM vm, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;

            // If we're starting the iteration, return the first index.
            if (args[1].Type == ValueType.Null)
            {
                if (s.Value.Length != 0)
                {
                    args[0] = new Value(0.0);
                    return PrimitiveResult.Value;
                }
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            if (args[1].Type == ValueType.Num)
            {
                if (args[1].Num < 0)
                {
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    index++;
                    if (index >= s.Value.Length)
                    {
                        args[0] = new Value(false);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value(index);
                    return PrimitiveResult.Value;
                }

                // Advance to the beginning of the next UTF-8 sequence.
                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }

            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_iterateByte(WrenVM vm, Value[] args)
        {
            Byte[] s = ((ObjString)args[0].Obj).GetBytes();

            // If we're starting the iteration, return the first index.
            if (args[1].Type == ValueType.Null)
            {
                if (s.Length == 0)
                {
                    args[0] = new Value(false);
                    return PrimitiveResult.Value;
                }
                args[0] = new Value(0.0);
                return PrimitiveResult.Value;
            }

            if (args[1].Type != ValueType.Num) return PrimitiveResult.Error;

            if (args[1].Num < 0)
            {
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }
            int index = (int)args[1].Num;

            // Advance to the next byte.
            index++;
            if (index >= s.Length)
            {
                args[0] = new Value(false);
                return PrimitiveResult.Value;
            }

            args[0] = new Value(index);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_iteratorValue(WrenVM vm, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (index < s.Value.Length && index >= 0)
                    {
                        args[0] = new Value("" + s.Value[index]);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Iterator out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Iterator must be an integer.");
                return PrimitiveResult.Error;
            }
            args[0] = new Value("Iterator must be a number.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_startsWith(WrenVM vm, Value[] args)
        {
            ObjString s = (ObjString)args[0].Obj;
            ObjString search = args[1].Obj as ObjString;

            if (search != null)
            {
                args[0] = new Value(s.Value.StartsWith(search.Value));
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Argument must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_toString(WrenVM vm, Value[] args)
        {
            return PrimitiveResult.Value;
        }

        static PrimitiveResult prim_string_plus(WrenVM vm, Value[] args)
        {
            ObjString s1 = args[1].Obj as ObjString;
            if (s1 != null)
            {
                args[0] = new Value(((ObjString)args[0].Obj).Value + s1.Value);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Right operand must be a string.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult prim_string_subscript(WrenVM vm, Value[] args)
        {
            string s = ((ObjString)args[0].Obj).Value;

            if (args[1].Type == ValueType.Num)
            {
                int index = (int)args[1].Num;

                if (index == args[1].Num)
                {
                    if (index < 0)
                    {
                        index += s.Length;
                    }

                    if (index >= 0 && index < s.Length)
                    {
                        args[0] = ObjString.FromCodePoint(s[index]);
                        return PrimitiveResult.Value;
                    }

                    args[0] = new Value("Subscript out of bounds.");
                    return PrimitiveResult.Error;
                }

                args[0] = new Value("Subscript must be an integer.");
                return PrimitiveResult.Error;
            }

            if (args[1].Obj as ObjRange != null)
            {
                ObjRange r = args[1].Obj as ObjRange;

                // TODO: This is seriously broken and needs a rewrite
                int from = (int)r.From;
                if (from != r.From)
                {
                    args[0] = new Value("Range start must be an integer.");
                    return PrimitiveResult.Error;
                }
                int to = (int)r.To;
                if (to != r.To)
                {
                    args[0] = new Value("Range end must be an integer.");
                    return PrimitiveResult.Error;
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
                    args[0] = new Value("Range end out of bounds.");
                    return PrimitiveResult.Error;
                }
                if (from < 0 || (from >= s.Length && from > 0))
                {
                    args[0] = new Value("Range start out of bounds.");
                    return PrimitiveResult.Error;
                }

                string result = "";
                for (int i = 0; i < count; i++)
                {
                    result += s[from + (i * step)];
                }

                args[0] = new Value(result);
                return PrimitiveResult.Value;
            }

            args[0] = new Value("Subscript must be a number or a range.");
            return PrimitiveResult.Error;
        }

        static PrimitiveResult WriteString(WrenVM vm, Value[] args)
        {
            if (args[1] != null && args[1].Type == ValueType.Obj)
            {
                string s = args[1].Obj.ToString();
                Console.Write(s);
            }
            args[0] = new Value(ValueType.Null);
            return PrimitiveResult.Value;
        }

        static PrimitiveResult Clock(WrenVM vm, Value[] args)
        {
            args[0] = new Value((double)DateTime.Now.Ticks / 10000000);
            return PrimitiveResult.Value;
        }

        // Creates either the Object or Class class in the core library with [name].
        static ObjClass DefineClass(WrenVM vm, string name)
        {
            ObjString nameString = new ObjString(name);

            ObjClass classObj = new ObjClass(0, nameString);

            vm.DefineVariable(null, name, new Value(classObj));

            return classObj;
        }

        static bool ValidateKey(Value arg)
        {
            return arg.Type == ValueType.False
                   || arg.Type == ValueType.True
                   || arg.Type == ValueType.Num
                   || arg.Type == ValueType.Null
                   || arg.Obj is ObjClass || arg.Obj is ObjFiber
                   || arg.Obj is ObjRange || arg.Obj is ObjString;
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
            _vm.Interpret("", CoreLibSource);

            WrenVM.BoolClass = (ObjClass)_vm.FindVariable("Bool").Obj;
            _vm.Primitive(WrenVM.BoolClass, "toString", prim_bool_toString);
            _vm.Primitive(WrenVM.BoolClass, "!", prim_bool_not);

            WrenVM.FiberClass = (ObjClass)_vm.FindVariable("Fiber").Obj;
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "new(_)", prim_fiber_new);
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "abort(_)", prim_fiber_abort);
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "current", prim_fiber_current);
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "yield()", prim_fiber_yield);
            _vm.Primitive(WrenVM.FiberClass.ClassObj, "yield(_)", prim_fiber_yield1);
            _vm.Primitive(WrenVM.FiberClass, "call()", prim_fiber_call);
            _vm.Primitive(WrenVM.FiberClass, "call(_)", prim_fiber_call1);
            _vm.Primitive(WrenVM.FiberClass, "error", prim_fiber_error);
            _vm.Primitive(WrenVM.FiberClass, "isDone", prim_fiber_isDone);
            _vm.Primitive(WrenVM.FiberClass, "run()", prim_fiber_run);
            _vm.Primitive(WrenVM.FiberClass, "run(_)", prim_fiber_run1);
            _vm.Primitive(WrenVM.FiberClass, "try()", prim_fiber_try);

            WrenVM.FnClass = (ObjClass)_vm.FindVariable("Fn").Obj;
            _vm.Primitive(WrenVM.FnClass.ClassObj, "new(_)", prim_fn_new);

            _vm.Primitive(WrenVM.FnClass, "arity", prim_fn_arity);
            _vm.Primitive(WrenVM.FnClass, "call()", prim_fn_call0);
            _vm.Primitive(WrenVM.FnClass, "call(_)", prim_fn_call1);
            _vm.Primitive(WrenVM.FnClass, "call(_,_)", prim_fn_call2);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_)", prim_fn_call3);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_)", prim_fn_call4);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_)", prim_fn_call5);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_)", prim_fn_call6);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_)", prim_fn_call7);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_)", prim_fn_call8);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_)", prim_fn_call9);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_)", prim_fn_call10);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call11);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call12);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call13);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call14);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call15);
            _vm.Primitive(WrenVM.FnClass, "call(_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_)", prim_fn_call16);
            _vm.Primitive(WrenVM.FnClass, "toString", prim_fn_toString);

            WrenVM.NullClass = (ObjClass)_vm.FindVariable("Null").Obj;
            _vm.Primitive(WrenVM.NullClass, "!", prim_null_not);
            _vm.Primitive(WrenVM.NullClass, "toString", prim_null_toString);

            WrenVM.NumClass = (ObjClass)_vm.FindVariable("Num").Obj;
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

            WrenVM.StringClass = (ObjClass)_vm.FindVariable("String").Obj;
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

            WrenVM.ListClass = (ObjClass)_vm.FindVariable("List").Obj;
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

            WrenVM.MapClass = (ObjClass)_vm.FindVariable("Map").Obj;
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

            WrenVM.RangeClass = (ObjClass)_vm.FindVariable("Range").Obj;
            _vm.Primitive(WrenVM.RangeClass, "from", prim_range_from);
            _vm.Primitive(WrenVM.RangeClass, "to", prim_range_to);
            _vm.Primitive(WrenVM.RangeClass, "min", prim_range_min);
            _vm.Primitive(WrenVM.RangeClass, "max", prim_range_max);
            _vm.Primitive(WrenVM.RangeClass, "isInclusive", prim_range_isInclusive);
            _vm.Primitive(WrenVM.RangeClass, "iterate(_)", prim_range_iterate);
            _vm.Primitive(WrenVM.RangeClass, "iteratorValue(_)", prim_range_iteratorValue);
            _vm.Primitive(WrenVM.RangeClass, "toString", prim_range_toString);

            ObjClass system = (ObjClass)_vm.FindVariable("System").Obj;
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
