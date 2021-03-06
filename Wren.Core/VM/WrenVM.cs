using System;
using System.Collections;
using System.Collections.Generic;
using Wren.Core.Bytecode;
using Wren.Core.Library;
using Wren.Core.Objects;

namespace Wren.Core.VM
{
    public delegate string WrenLoadModuleFn(string name);

    public delegate bool Primitive(WrenVM vm, Obj[] args, int stackStart);
    public delegate IEnumerator PrimitiveCoroutine(WrenVM vm, Obj[] args, int stackStart, WrenVM.SuccessRef succeeded);

    public enum MethodType
    {
        // A primitive method implemented in the VM.
        // this can directly manipulate the fiber's stack.
        Primitive,
		PrimitiveCoroutine,

        // A normal user-defined method.
        Block,

        // Special Call Method
        Call,
	};

    public class Method
    {
        public MethodType MType;

        // The method function itself. The [type] determines which field of the union
        // is used.
        public Primitive Primitive;
        public PrimitiveCoroutine PrimitiveCoroutine;

        // May be a [ObjFn] or [ObjClosure].
        public Obj Obj;
    } ;

    public enum InterpretResult
    {
        Success = 0,
        CompileError = 65,
        RuntimeError = 70
    } ;

    public class WrenVM
    {
		public class SuccessRef
		{
			public bool value = true;
		}

		public class ResultRef
		{
			public InterpretResult value = InterpretResult.Success;
		}

		public static ObjClass BoolClass;
        public static ObjClass ClassClass;
        public static ObjClass FiberClass;
        public static ObjClass FnClass;
        public static ObjClass ListClass;
        public static ObjClass MapClass;
        public static ObjClass NullClass;
        public static ObjClass NumClass;
        public static ObjClass ObjectClass;
        public static ObjClass RangeClass;
        public static ObjClass StringClass;
        public static ObjClass ForeignClass;

        // The fiber that is currently running.
        public ObjFiber Fiber;

        readonly ObjMap _modules;

		public WrenVM( Action<string> write, Action<string> error )
		{
			Write = write ?? (_ => Console.WriteLine( _ ));
			Error = error ?? (_ => Console.Error.WriteLine( _ ));

			MethodNames = new List<string>();
            ObjString name = new ObjString("core");

            // Implicitly create a "core" module for the built in libraries.
            ObjModule coreModule = new ObjModule(name);

            _modules = new ObjMap();
            _modules.Set(Obj.Null, coreModule);

            CoreLibrary core = new CoreLibrary(this);
            core.InitializeCore();

            // Load in System functions
            Meta.LoadLibrary(this);
        }

        public List<string> MethodNames;

        public Compiler Compiler { get; set; }

        public WrenLoadModuleFn LoadModuleFn { get; set; }

		public Action<string> Write { get; private set; }
		public Action<string> Error { get; private set; }

        // Defines [methodValue] as a method on [classObj].
        private static bool BindMethod(bool isStatic, int symbol, ObjClass classObj, Obj methodContainer)
        {
            // If we are binding a foreign method, just return, as this will be handled later
            if (methodContainer is ObjString)
                return true;

            ObjFn methodFn = methodContainer as ObjFn ?? ((ObjClosure)methodContainer).Function;

            Method method = new Method { MType = MethodType.Block, Obj = methodContainer };

            if (isStatic)
                classObj = classObj.ClassObj;

            // Methods are always bound against the class, and not the metaclass, even
            // for static methods, because static methods don't have instance fields
            // anyway.
            Compiler.BindMethodCode(classObj, methodFn);

            classObj.BindMethod(symbol, method);
            return true;
        }

        // Creates a string containing an appropriate method not found error for a
        // method with [symbol] on [classObj].
        static void MethodNotFound(WrenVM vm, ObjClass classObj, int symbol)
        {
            vm.Fiber.Error = Obj.MakeString(string.Format("{0} does not implement '{1}'.", classObj.Name, vm.MethodNames[symbol]));
        }

        // Looks up the previously loaded module with [name].
        // Returns null if no module with that name has been loaded.
        private ObjModule GetModule(Obj name)
        {
            Obj moduleContainer = _modules.Get(name);
            return moduleContainer == Obj.Undefined ? null : moduleContainer as ObjModule;
        }

        private ObjModule GetModuleByName(string name)
        {
            for (int i = 1; i < _modules.Count(); i++)
            {
                Obj v = _modules.GetKey(i);
                if (v as ObjString != null && (v as ObjString).Str == name)
                    return _modules.Get(i) as ObjModule;
            }
            return null;
        }

        // Looks up the core module in the module map.
        private ObjModule GetCoreModule()
        {
            return GetModule(Obj.Null);
        }

        private ObjFiber LoadModule(Obj name, string source)
        {
            ObjModule module = GetModule(name);

            // See if the module has already been loaded.
            if (module == null)
            {
                module = new ObjModule(name as ObjString);

                // Store it in the VM's module registry so we don't load the same module
                // multiple times.
                _modules.Set(name, module);

                // Implicitly import the core module.
                ObjModule coreModule = GetCoreModule();
                foreach (ModuleVariable t in coreModule.Variables)
                {
                    DefineVariable(module, t.Name, t.Container);
                }
            }

            ObjFn fn = Compiler.Compile(this, module, name.ToString(), source, true);
            if (fn == null)
            {
                // TODO: Should we still store the module even if it didn't compile?
                return null;
            }

            ObjFiber moduleFiber = new ObjFiber(fn);

            // Return the fiber that executes the module.
            return moduleFiber;
        }

        private Obj ImportModule(Obj name)
        {
            // If the module is already loaded, we don't need to do anything.
            if (_modules.Get(name) != Obj.Undefined) return Obj.Null;

            // Load the module's source code from the embedder.
            string source = LoadModuleFn(name.ToString());
            if (source == null)
            {
                // Couldn't load the module.
                return Obj.MakeString(string.Format("Could not find module '{0}'.", name));
            }

            ObjFiber moduleFiber = LoadModule(name, source);

            // Return the fiber that executes the module.
            return moduleFiber;
        }


        private bool ImportVariable(Obj moduleName, Obj variableName, out Obj result)
        {
            ObjModule module = GetModule(moduleName);
            if (module == null)
            {
                result = Obj.MakeString("Could not load module");
                return false; // Should only look up loaded modules
            }

            ObjString variable = variableName as ObjString;
            if (variable == null)
            {
                result = Obj.MakeString("Variable name must be a string");
                return false;
            }

            int variableEntry = module.Variables.FindIndex(v => v.Name == variable.ToString());

            // It's a runtime error if the imported variable does not exist.
            if (variableEntry == -1)
            {
                result = Obj.MakeString(string.Format("Could not find a variable named '{0}' in module '{1}'.", variableName, moduleName));
                return false;
            }

            result = module.Variables[variableEntry].Container;
            return true;
        }

        // Verifies that [superclass] is a valid object to inherit from. That means it
        // must be a class and cannot be the class of any built-in type.
        //
        // If successful, returns null. Otherwise, returns a string for the runtime
        // error message.
        private static Obj ValidateSuperclass(Obj name, Obj superclassContainer)
        {
            // Make sure the superclass is a class.
            ObjClass superClass = superclassContainer as ObjClass;
            if (superClass != null)
            {
                // Make sure it doesn't inherit from a sealed built-in type. Primitive methods
                // on these classes assume the instance is one of the other Obj___ types and
                // will fail horribly if it's actually an ObjInstance.
                return superClass.IsSealed ? Obj.MakeString(string.Format("Class '{0}' cannot inherit from built-in class '{1}'.", name as ObjString, (superClass.Name))) : null;
            }

            return Obj.MakeString(string.Format("Class '{0}' cannot inherit from a non-class object.", name));
        }

		// The main bytecode interpreter loop. This is where the magic happens. It is
		// also, as you can imagine, highly performance critical. Returns `true` if the
		// fiber completed without error.
		private IEnumerator RunInterpreter(SuccessRef succeeded)
		{
			/* Load Frame */
			CallFrame frame = Fiber.Frames[Fiber.NumFrames - 1];
            int ip = frame.Ip;
            int stackStart = frame.StackStart;
            Obj[] stack = Fiber.Stack;

            ObjFn fn = frame.Fn as ObjFn ?? ((ObjClosure)frame.Fn).Function;
            byte[] bytecode = fn.Bytecode;

            while (true)
            {
                Instruction instruction = (Instruction)bytecode[ip++];
                int index;
                switch (instruction)
                {
                    case Instruction.LOAD_LOCAL_0:
                    case Instruction.LOAD_LOCAL_1:
                    case Instruction.LOAD_LOCAL_2:
                    case Instruction.LOAD_LOCAL_3:
                    case Instruction.LOAD_LOCAL_4:
                    case Instruction.LOAD_LOCAL_5:
                    case Instruction.LOAD_LOCAL_6:
                    case Instruction.LOAD_LOCAL_7:
                    case Instruction.LOAD_LOCAL_8:
                        {
                            index = stackStart + instruction - Instruction.LOAD_LOCAL_0;
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = stack[index];
                            break;
                        }

                    case Instruction.LOAD_LOCAL:
                        {
                            index = stackStart + bytecode[ip++];
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = stack[index];
                            break;
                        }

                    case Instruction.LOAD_FIELD_THIS:
                        {
                            byte field = bytecode[ip++];
                            ObjInstance instance = (ObjInstance)stack[stackStart];
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = instance.Fields[field];
                            break;
                        }

                    case Instruction.POP:
                        {
                            Fiber.StackTop--;
                            break;
                        }

                    case Instruction.DUP:
                        {
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop] = stack[Fiber.StackTop - 1];
                            Fiber.StackTop++;
                            break;
                        }

                    case Instruction.NULL:
                        {
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = Obj.Null;
                            break;
                        }

                    case Instruction.FALSE:
                        {
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = Obj.False;
                            break;
                        }

                    case Instruction.TRUE:
                        {
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = Obj.True;
                            break;
                        }

                    case Instruction.CALL_0:
                    case Instruction.CALL_1:
                    case Instruction.CALL_2:
                    case Instruction.CALL_3:
                    case Instruction.CALL_4:
                    case Instruction.CALL_5:
                    case Instruction.CALL_6:
                    case Instruction.CALL_7:
                    case Instruction.CALL_8:
                    case Instruction.CALL_9:
                    case Instruction.CALL_10:
                    case Instruction.CALL_11:
                    case Instruction.CALL_12:
                    case Instruction.CALL_13:
                    case Instruction.CALL_14:
                    case Instruction.CALL_15:
                    case Instruction.CALL_16:
                    // Handle Super calls
                    case Instruction.SUPER_0:
                    case Instruction.SUPER_1:
                    case Instruction.SUPER_2:
                    case Instruction.SUPER_3:
                    case Instruction.SUPER_4:
                    case Instruction.SUPER_5:
                    case Instruction.SUPER_6:
                    case Instruction.SUPER_7:
                    case Instruction.SUPER_8:
                    case Instruction.SUPER_9:
                    case Instruction.SUPER_10:
                    case Instruction.SUPER_11:
                    case Instruction.SUPER_12:
                    case Instruction.SUPER_13:
                    case Instruction.SUPER_14:
                    case Instruction.SUPER_15:
                    case Instruction.SUPER_16:
                        {
                            int numArgs = instruction - (instruction >= Instruction.SUPER_0 ? Instruction.SUPER_0 : Instruction.CALL_0) + 1;
                            int symbol = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;

                            // The receiver is the first argument.
                            int argStart = Fiber.StackTop - numArgs;
                            Obj receiver = stack[argStart];
                            ObjClass classObj;

                            if (instruction < Instruction.SUPER_0)
                            {
                                if (receiver.Type == ObjType.Obj)
                                    classObj = receiver.ClassObj;
                                else if (receiver.Type == ObjType.Num)
                                    classObj = NumClass;
                                else if (receiver == Obj.True || receiver == Obj.False)
                                    classObj = BoolClass;
                                else
                                    classObj = NullClass;
                            }
                            else
                            {
                                // The superclass is stored in a constant.
                                classObj = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]] as ObjClass;
                                ip += 2;
                            }

                            // If the class's method table doesn't include the symbol, bail.
                            Method method = symbol < classObj.Methods.Length ? classObj.Methods[symbol] : null;

                            if (method == null)
                            {
                                /* Method not found */
                                frame.Ip = ip;
                                MethodNotFound(this, classObj, symbol);
                                if (!HandleRuntimeError())
                                    { succeeded.value = false; yield break; }
                                frame = Fiber.Frames[Fiber.NumFrames - 1];
                                ip = frame.Ip;
                                stackStart = frame.StackStart;
                                stack = Fiber.Stack;
                                fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }

                            if (method.MType == MethodType.Primitive)
                            {
                                // After calling this, the result will be in the first arg slot.
                                if (method.Primitive(this, stack, argStart))
                                {
                                    Fiber.StackTop = argStart + 1;
                                }
                                else
                                {
                                    frame.Ip = ip;

                                    if (Fiber.Error != null && Fiber.Error != Obj.Null)
                                    {
                                        if (!HandleRuntimeError())
                                            { succeeded.value = false; yield break; }
                                    }
                                    else
                                    {
                                        // If we don't have a fiber to switch to, stop interpreting.
                                        if (stack[argStart] == Obj.Null)
                                            { succeeded.value = true; yield break; }
                                        Fiber = stack[argStart] as ObjFiber;
                                        if (Fiber == null)
                                            { succeeded.value = false; yield break; }
                                    }

                                    /* Load Frame */
                                    frame = Fiber.Frames[Fiber.NumFrames - 1];
                                    ip = frame.Ip;
                                    stackStart = frame.StackStart;
                                    stack = Fiber.Stack;
                                    fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                    bytecode = fn.Bytecode;
                                }
                                break;
                            }

                            if (method.MType == MethodType.PrimitiveCoroutine)
                            {
								var coroutine = method.PrimitiveCoroutine( this, stack, argStart, succeeded );
								while( coroutine.MoveNext() )
								{
									yield return coroutine.Current;
								}

								// After calling this, the result will be in the first arg slot.
								if( succeeded.value )
                                {
                                    Fiber.StackTop = argStart + 1;
                                }
                                else
                                {
                                    frame.Ip = ip;

                                    if (Fiber.Error != null && Fiber.Error != Obj.Null)
                                    {
                                        if (!HandleRuntimeError())
                                            { succeeded.value = false; yield break; }
                                    }
                                    else
                                    {
                                        // If we don't have a fiber to switch to, stop interpreting.
                                        if (stack[argStart] == Obj.Null)
                                            { succeeded.value = true; yield break; }
                                        Fiber = stack[argStart] as ObjFiber;
                                        if (Fiber == null)
                                            { succeeded.value = false; yield break; }
                                    }

                                    /* Load Frame */
                                    frame = Fiber.Frames[Fiber.NumFrames - 1];
                                    ip = frame.Ip;
                                    stackStart = frame.StackStart;
                                    stack = Fiber.Stack;
                                    fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                    bytecode = fn.Bytecode;
                                }
                                break;
                            }

                            frame.Ip = ip;

                            if (method.MType == MethodType.Block)
                            {
                                receiver = method.Obj;
                            }
                            else if (!CheckArity(stack, numArgs, argStart))
                            {
                                if (!HandleRuntimeError())
                                    { succeeded.value = false; yield break; }

                                frame = Fiber.Frames[Fiber.NumFrames - 1];
                                ip = frame.Ip;
                                stackStart = frame.StackStart;
                                stack = Fiber.Stack;
                                fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }

                            Fiber.Frames.Add(frame = new CallFrame { Fn = receiver, StackStart = argStart, Ip = 0 });
                            Fiber.NumFrames++;
                            /* Load Frame */
                            ip = 0;
                            stackStart = argStart;
                            fn = (receiver as ObjFn) ?? (receiver as ObjClosure).Function;
                            bytecode = fn.Bytecode;
                            break;
                        }

                    case Instruction.STORE_LOCAL:
                        {
                            index = stackStart + bytecode[ip++];
                            stack[index] = stack[Fiber.StackTop - 1];
                            break;
                        }

                    case Instruction.CONSTANT:
                        {
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]];
                            ip += 2;
                            break;
                        }

                    case Instruction.LOAD_UPVALUE:
                        {
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = ((ObjClosure)frame.Fn).Upvalues[bytecode[ip++]].Container;
                            break;
                        }

                    case Instruction.STORE_UPVALUE:
                        {
                            ObjUpvalue[] upvalues = ((ObjClosure)frame.Fn).Upvalues;
                            upvalues[bytecode[ip++]].Container = stack[Fiber.StackTop - 1];
                            break;
                        }

                    case Instruction.LOAD_MODULE_VAR:
                        {
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = fn.Module.Variables[(bytecode[ip] << 8) + bytecode[ip + 1]].Container;
                            ip += 2;
                            break;
                        }

                    case Instruction.STORE_MODULE_VAR:
                        {
                            fn.Module.Variables[(bytecode[ip] << 8) + bytecode[ip + 1]].Container = stack[Fiber.StackTop - 1];
                            ip += 2;
                            break;
                        }

                    case Instruction.STORE_FIELD_THIS:
                        {
                            byte field = bytecode[ip++];
                            ObjInstance instance = (ObjInstance)stack[stackStart];
                            instance.Fields[field] = stack[Fiber.StackTop - 1];
                            break;
                        }

                    case Instruction.LOAD_FIELD:
                        {
                            byte field = bytecode[ip++];
                            ObjInstance instance = (ObjInstance)stack[--Fiber.StackTop];
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = instance.Fields[field];
                            break;
                        }

                    case Instruction.STORE_FIELD:
                        {
                            byte field = bytecode[ip++];
                            ObjInstance instance = (ObjInstance)stack[--Fiber.StackTop];
                            instance.Fields[field] = stack[Fiber.StackTop - 1];
                            break;
                        }

                    case Instruction.JUMP:
                        {
                            int offset = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += offset + 2;
                            break;
                        }

                    case Instruction.LOOP:
                        {
                            // Jump back to the top of the loop.
                            int offset = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;
                            ip -= offset;
                            break;
                        }

                    case Instruction.JUMP_IF:
                        {
                            int offset = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;
                            Obj condition = stack[--Fiber.StackTop];

                            if (condition == Obj.False || condition == Obj.Null) ip += offset;
                            break;
                        }

                    case Instruction.AND:
                    case Instruction.OR:
                        {
                            int offset = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;
                            Obj condition = stack[Fiber.StackTop - 1];

                            if ((condition == Obj.Null || condition == Obj.False) ^ instruction == Instruction.OR)
                                ip += offset;
                            else
                                Fiber.StackTop--;
                            break;
                        }

                    case Instruction.CLOSE_UPVALUE:
                        {
                            Fiber.CloseUpvalue();
                            Fiber.StackTop--;
                            break;
                        }

                    case Instruction.RETURN:
                        {
                            Fiber.Frames.RemoveAt(--Fiber.NumFrames);
                            Obj result = stack[--Fiber.StackTop];
                            // Close any upvalues still in scope.
                            if (Fiber.StackTop > stackStart)
                            {
                                while (Fiber.OpenUpvalues != null && Fiber.OpenUpvalues.Index >= stackStart)
                                {
                                    Fiber.CloseUpvalue();
                                }
                            }

                            // If the fiber is complete, end it.
                            if (Fiber.NumFrames == 0)
                            {
                                // If this is the main fiber, we're done.
                                if (Fiber.Caller == null)
                                    { succeeded.value = true; yield break; }

                                // We have a calling fiber to resume.
                                Fiber = Fiber.Caller;
                                stack = Fiber.Stack;
                                // Store the result in the resuming fiber.
                                stack[Fiber.StackTop - 1] = result;
                            }
                            else
                            {
                                // Discard the stack slots for the call frame (leaving one slot for the result).
                                Fiber.StackTop = stackStart + 1;

                                // Store the result of the block in the first slot, which is where the
                                // caller expects it.
                                stack[stackStart] = result;
                            }

                            /* Load Frame */
                            frame = Fiber.Frames[Fiber.NumFrames - 1];
                            ip = frame.Ip;
                            stackStart = frame.StackStart;
                            fn = frame.Fn as ObjFn ?? (frame.Fn as ObjClosure).Function;
                            bytecode = fn.Bytecode;
                            break;
                        }

                    case Instruction.CLOSURE:
                        {
                            ObjFn prototype = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]] as ObjFn;
                            ip += 2;

                            // Create the closure and push it on the stack before creating upvalues
                            // so that it doesn't get collected.
                            ObjClosure closure = new ObjClosure(prototype);
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = closure;

                            // Capture upvalues.
                            for (int i = 0; i < prototype.NumUpvalues; i++)
                            {
                                byte isLocal = bytecode[ip++];
                                index = bytecode[ip++];
                                if (isLocal > 0)
                                {
                                    // Make an new upvalue to close over the parent's local variable.
                                    closure.Upvalues[i] = Fiber.CaptureUpvalue(stackStart + index);
                                }
                                else
                                {
                                    // Use the same upvalue as the current call frame.
                                    closure.Upvalues[i] = ((ObjClosure)frame.Fn).Upvalues[index];
                                }
                            }

                            break;
                        }

                    case Instruction.CLASS:
                        {
                            Obj name = stack[Fiber.StackTop - 2];
                            ObjClass superclass = stack[Fiber.StackTop - 1] as ObjClass;

                            Obj error = ValidateSuperclass(name, stack[Fiber.StackTop - 1]);
                            if (error != null)
                            {
                                Fiber.Error = error;
                                frame.Ip = ip;
                                if (!HandleRuntimeError())
                                    { succeeded.value = false; yield break; }
                                /* Load Frame */
                                frame = Fiber.Frames[Fiber.NumFrames - 1];
                                ip = frame.Ip;
                                stackStart = frame.StackStart;
                                stack = Fiber.Stack;
                                fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }

                            int numFields = bytecode[ip++];

                            Obj classObj = new ObjClass(superclass, numFields, name as ObjString);

                            // Don't pop the superclass and name off the stack until the subclass is
                            // done being created, to make sure it doesn't get collected.
                            Fiber.StackTop -= 2;

                            // Now that we know the total number of fields, make sure we don't overflow.
                            if (superclass.NumFields + numFields <= Compiler.MaxFields)
                            {
                                stack[Fiber.StackTop++] = classObj;
                                break;
                            }

                            // Overflow handling
                            frame.Ip = ip;
                            Fiber.Error = Obj.MakeString(string.Format("Class '{0}' may not have more than 255 fields, including inherited ones.", name));
                            if (!HandleRuntimeError())
                                { succeeded.value = false; yield break; }
                            /* Load Frame */
                            frame = Fiber.Frames[Fiber.NumFrames - 1];
                            ip = frame.Ip;
                            stackStart = frame.StackStart;
                            stack = Fiber.Stack;
                            fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                            bytecode = fn.Bytecode;
                            break;
                        }

                    case Instruction.METHOD_INSTANCE:
                    case Instruction.METHOD_STATIC:
                        {
                            int symbol = (bytecode[ip] << 8) + bytecode[ip + 1];
                            ip += 2;
                            ObjClass classObj = stack[Fiber.StackTop - 1] as ObjClass;
                            Obj method = stack[Fiber.StackTop - 2];
                            bool isStatic = instruction == Instruction.METHOD_STATIC;
                            if (!BindMethod(isStatic, symbol, classObj, method))
                            {
                                frame.Ip = ip;
                                Fiber.Error = Obj.MakeString("Error while binding method");
                                if (!HandleRuntimeError())
                                    { succeeded.value = false; yield break; }
                                /* Load Frame */
                                frame = Fiber.Frames[Fiber.NumFrames - 1];
                                ip = frame.Ip;
                                stackStart = frame.StackStart;
                                stack = Fiber.Stack;
                                fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }
                            Fiber.StackTop -= 2;
                            break;
                        }

                    case Instruction.LOAD_MODULE:
                        {
                            Obj name = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]];
                            ip += 2;
                            Obj result = ImportModule(name);

                            // If it returned a string, it was an error message.
                            if ((result is ObjString))
                            {
                                frame.Ip = ip;
                                Fiber.Error = result;
                                if (!HandleRuntimeError())
                                    { succeeded.value = false; yield break; }
                                /* Load Frame */
                                frame = Fiber.Frames[Fiber.NumFrames - 1];
                                ip = frame.Ip;
                                stackStart = frame.StackStart;
                                stack = Fiber.Stack;
                                fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }

                            // Make a slot that the module's fiber can use to store its result in.
                            // It ends up getting discarded, but CODE_RETURN expects to be able to
                            // place a value there.
                            if (Fiber.StackTop >= Fiber.Capacity)
                                stack = Fiber.IncreaseStack();
                            stack[Fiber.StackTop++] = Obj.Null;

                            // If it returned a fiber to execute the module body, switch to it.
                            if (result is ObjFiber)
                            {
                                // Return to this module when that one is done.
                                (result as ObjFiber).Caller = Fiber;

                                frame.Ip = ip;
                                Fiber = (result as ObjFiber);
                                /* Load Frame */
                                frame = Fiber.Frames[Fiber.NumFrames - 1];
                                ip = frame.Ip;
                                stackStart = frame.StackStart;
                                stack = Fiber.Stack;
                                fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                            }

                            break;
                        }

                    case Instruction.IMPORT_VARIABLE:
                        {
                            Obj module = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]];
                            ip += 2;
                            Obj variable = fn.Constants[(bytecode[ip] << 8) + bytecode[ip + 1]];
                            ip += 2;
                            Obj result;
                            if (ImportVariable(module, variable, out result))
                            {
                                if (Fiber.StackTop >= Fiber.Capacity)
                                    stack = Fiber.IncreaseStack();
                                stack[Fiber.StackTop++] = result;
                            }
                            else
                            {
                                frame.Ip = ip;
                                Fiber.Error = result;
                                if (!HandleRuntimeError())
                                    { succeeded.value = false; yield break; }
                                /* Load Frame */
                                frame = Fiber.Frames[Fiber.NumFrames - 1];
                                ip = frame.Ip;
                                stackStart = frame.StackStart;
                                stack = Fiber.Stack;
                                fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                            }
                            break;
                        }

                    case Instruction.CONSTRUCT:
                        {
                            int stackPosition = Fiber.StackTop - 1 + (Instruction.CALL_0 - (Instruction)bytecode[ip]);
                            ObjClass v = stack[stackPosition] as ObjClass;
                            if (v == null)
                            {
                                Fiber.Error = Obj.MakeString("'this' should be a class.");
                                if (!HandleRuntimeError())
                                    { succeeded.value = false; yield break; }
                                /* Load Frame */
                                frame = Fiber.Frames[Fiber.NumFrames - 1];
                                ip = frame.Ip;
                                stackStart = frame.StackStart;
                                stack = Fiber.Stack;
                                fn = (frame.Fn as ObjFn) ?? (frame.Fn as ObjClosure).Function;
                                bytecode = fn.Bytecode;
                                break;
                            }
                            stack[stackPosition] = new ObjInstance(v);
                        }
                        break;

                    case Instruction.FOREIGN_CLASS:
                        // Not yet implemented
                        break;

                    case Instruction.FOREIGN_CONSTRUCT:
                        // Not yet implemented
                        break;

                    case Instruction.END:
                        // A CODE_END should always be preceded by a CODE_RETURN. If we get here,
                        // the compiler generated wrong code.
                        { succeeded.value = false; yield break; }
                }
            }

            // We should only exit this function from an explicit return from CODE_RETURN
            // or a runtime error.
        }

        // Execute [source] in the context of the core module.
        private IEnumerator LoadIntoCore(string source, ResultRef resultRef)
        {
            ObjModule coreModule = GetCoreModule();

            ObjFn fn = Compiler.Compile(this, coreModule, "", source, true);
			if( fn == null )
			{
				resultRef.value = InterpretResult.CompileError;
				yield break;
			}

            Fiber = new ObjFiber(fn);

			var succeeded = new SuccessRef();
			var interpreter = RunInterpreter(succeeded);
			while (interpreter.MoveNext())
			{
				yield return interpreter.Current;
			}

			resultRef.value = succeeded.value ? InterpretResult.Success : InterpretResult.RuntimeError;
        }

		public InterpretResult Interpret(string moduleName, string sourcePath, string source)
		{
			var resultRef = new ResultRef();
			var interpreter = InterpretCoroutines( moduleName, sourcePath, source, resultRef );
			// var result = InterpretResult.Running;
			while( interpreter.MoveNext() ) { }
			return resultRef.value;
		}

        public IEnumerator InterpretCoroutines(string moduleName, string sourcePath, string source, ResultRef resultRef)
        {
			if( sourcePath.Length == 0 )
			{
				var loader = LoadIntoCore( source, resultRef );
				while (loader.MoveNext())
				{
					yield return loader.Current;
				}
				yield break;
			}

            // TODO: Better module name.
            Obj name = Obj.MakeString(moduleName);

            ObjFiber f = LoadModule(name, source);
            if (f == null)
            {
				resultRef.value = InterpretResult.CompileError;
				yield break;
            }

            Fiber = f;

			var succeeded = new SuccessRef();
			var interpreter = RunInterpreter(succeeded);
			while (interpreter.MoveNext())
			{
				yield return interpreter.Current;
			}

            resultRef.value = succeeded.value ? InterpretResult.Success : InterpretResult.RuntimeError;
        }

        public Obj FindVariable(string name)
        {
            ObjModule coreModule = GetCoreModule();
            int symbol = coreModule.Variables.FindIndex(v => v.Name == name);
            return coreModule.Variables[symbol].Container;
        }

        public Obj FindVariable(string moduleName, string name)
        {
            ObjModule m = GetModuleByName(moduleName);
            if (m == null)
                return Obj.Undefined;
            int symbol = m.Variables.FindIndex(v => v.Name == name);
            return m.Variables[symbol].Container;
        }

        internal int DeclareVariable(ObjModule module, string name)
        {
            if (module == null) module = GetCoreModule();
            if (module.Variables.Count == ObjModule.MaxModuleVars) return -2;

            module.Variables.Add(new ModuleVariable { Name = name, Container = Obj.Undefined });
            return module.Variables.Count - 1;
        }

        internal int DefineVariable(ObjModule module, string name, Obj c)
        {
            if (module == null) module = GetCoreModule();
            if (module.Variables.Count == ObjModule.MaxModuleVars) return -2;

            // See if the variable is already explicitly or implicitly declared.
            int symbol = module.Variables.FindIndex(m => m.Name == name);

            if (symbol == -1)
            {
                // Brand new variable.
                module.Variables.Add(new ModuleVariable { Name = name, Container = c });
                symbol = module.Variables.Count - 1;
            }
            else if (module.Variables[symbol].Container == Obj.Undefined)
            {
                // Explicitly declaring an implicitly declared one. Mark it as defined.
                module.Variables[symbol].Container = c;
            }
            else
            {
                // Already explicitly declared.
                symbol = -1;
            }

            return symbol;
        }

        /* Dirty Hack */
        private bool HandleRuntimeError()
        {
            ObjFiber f = Fiber;
            if (f.CallerIsTrying)
            {
                f.Caller.SetReturnValue(f.Error);
                Fiber = f.Caller;
                return true;
            }
            Fiber = null;

            // TODO: Fix this so that there is no dependancy on the console
            if (!(f.Error is ObjString))
            {
                f.Error = Obj.MakeString("Error message must be a string.");
            }

            Error((f.Error as ObjString).Str);
            return false;
        }

        public void Primitive(ObjClass objClass, string s, Primitive func)
        {
            if (!MethodNames.Contains(s))
            {
                MethodNames.Add(s);
            }
            int symbol = MethodNames.IndexOf(s);

            objClass.BindMethod(symbol, new Method { Primitive = func, MType = MethodType.Primitive });
        }

        public void Primitive(ObjClass objClass, string s, PrimitiveCoroutine func)
        {
            if (!MethodNames.Contains(s))
            {
                MethodNames.Add(s);
            }
            int symbol = MethodNames.IndexOf(s);

            objClass.BindMethod(symbol, new Method { PrimitiveCoroutine = func, MType = MethodType.PrimitiveCoroutine });
        }

        public void Call(ObjClass objClass, string s)
        {
            if (!MethodNames.Contains(s))
            {
                MethodNames.Add(s);
            }
            int symbol = MethodNames.IndexOf(s);

            objClass.BindMethod(symbol, new Method { MType = MethodType.Call });
        }

        bool CheckArity(Obj[] args, int numArgs, int stackStart)
        {
            ObjFn fn = args[stackStart] as ObjFn;
            ObjClosure c = args[stackStart] as ObjClosure;

            if (c != null)
            {
                fn = c.Function;
            }

            if (fn == null)
            {
                Fiber.Error = Obj.MakeString("Receiver must be a function or closure.");
                return false;
            }

            if (numArgs - 1 < fn.Arity)
            {
                Fiber.Error = Obj.MakeString("Function expects more arguments.");
                return false;
            }

            return true;
        }

    }
}
