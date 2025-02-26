using System;
using Wasmtime;
using Newtonsoft.Json.Linq;
using static smwasm.Smh;

namespace smwasm
{
    public class WasmItem : ISmItem
    {
        static int gsn = 0;

        public String wasmPath;
        public int maxPage;
        int sn;

        System.Func<int, int> smalloc;
        System.Action<int> smdealloc;
        System.Func<int, int, int> smcall;
        Wasmtime.Memory mem;

        public WasmItem(String wasmPath, int maxPage)
        {
            gsn++;
            this.sn = gsn;

            this.wasmPath = wasmPath;
            this.maxPage = maxPage;
        }

        public long HostGetMs()
        {
            long ms = (long)DateTime.Now.Millisecond;
            return ms;
        }

        public void HostDebug(int d1, int d2)
        {
            Console.WriteLine("+++ {0} --- < < --- {1} --- {2} ---", this.sn, d1, d2);
        }

        public void HostPutMemory(int ptr, int ty)
        {
            if (ty != 10)
            {
                return;
            }
            String txt = this.GetOutput(ptr);
            Console.WriteLine("+++ {0} {1}", this.sn, txt);
        }

        public int HostCallSm(int ptr)
        {
            try
            {
                String txt = this.GetOutput(ptr);
                JObject dict = JObject.Parse(txt);
                String result = Smh.Call(txt);
                int ptr_ret = this.SetInput(result);
                return ptr_ret;
            }
            catch (Exception e)
            {
            }
            return 0;
        }

        public String GetOutput(int ptr)
        {
            try
            {
                int len = this.mem.ReadInt32(ptr);
                String txt = this.mem.ReadString(ptr + 4, len);
                return txt;
            }
            catch (Exception e)
            {
            }
            return "{}";
        }

        public int SetInput(String txt)
        {
            int len = txt.Length;
            int ptr = this.smalloc(len);
            this.mem.WriteString(ptr + 4, txt);
            return ptr;
        }

        public String Call(String input)
        {
            int ptr = this.SetInput(input);
            int ptr_ret = this.smcall(ptr, 1);
            String txt = this.GetOutput(ptr_ret);
            this.smdealloc(ptr_ret);
            return txt;
        }

        public void Load()
        {
            var engine = new Engine();
            var linker = new Linker(engine);
            var store = new Store(engine);

            var module = Module.FromFile(engine, this.wasmPath);
            var imports = module.Imports;
            for (int i = 0; i < imports.Count; i++)
            {
                var imp = imports[i];
                String name = imp.Name;
                if (name.StartsWith("__wbg_"))
                {
                    name = name.Substring(6, name.Length - 23);
                }

                if (name == "hostdebug")
                {
                    linker.Define(imp.ModuleName, imp.Name,
                        Function.FromCallback(store, (Action<int, int>)this.HostDebug));
                }
                else if (name == "hostgetms")
                {
                    linker.Define(imp.ModuleName, imp.Name,
                        Function.FromCallback<long>(store, this.HostGetMs));
                }
                else if (name == "hostputmemory")
                {
                    linker.Define(imp.ModuleName, imp.Name,
                        Function.FromCallback(store, (Action<int, int>)this.HostPutMemory));
                }
                else if (name == "hostcallsm")
                {
                    linker.Define(imp.ModuleName, imp.Name,
                        Function.FromCallback<int, int>(store, this.HostCallSm));
                }
            }

            var instance = linker.Instantiate(store, module);

            var sminit = instance.GetFunction<int, int>("sminit");
            this.smalloc = instance.GetFunction<int, int>("smalloc");
            this.smdealloc = instance.GetAction<int>("smdealloc");
            this.smcall = instance.GetFunction<int, int, int>("smcall");
            if (this.smalloc is null || this.smdealloc is null || sminit is null || this.smcall is null)
            {
                return;
            }

            this.mem = instance.GetMemory("memory");

            sminit(0);

            String output = this.Call("{\"$usage\": \"smker.get.all\"}");

            try
            {
                JObject dict = JObject.Parse(output);
                foreach (var property in dict.Properties())
                {
                    if (!property.Name.StartsWith("smker."))
                    {
                        Smh.RegisterItem(property.Name, this);
                    }
                }
            }
            catch (Exception e)
            {
            }
        }
    }
}
