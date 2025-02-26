using Newtonsoft.Json.Linq;
using System;
using System.Collections;


namespace smwasm
{

    public class Smh
    {
        public interface ISmItem
        {
            String Call(String input);
        }

        static Hashtable wasmFuncs = new Hashtable();

        public static void LoadWasm(String wasmPath, int maxPage)
        {
            var item = new WasmItem(wasmPath, maxPage);
            item.Load();
        }

        public static String Call(String input)
        {
            JObject dict = JObject.Parse(input);
            JToken value = dict["$usage"];
            String usage = value.Value<string>();

            ISmItem wi = (ISmItem)wasmFuncs[usage];
            if (wi == null)
            {
                return "{}";
            }
            var output = wi.Call(input);
            return output;
        }

        public static void RegisterItem(String key, ISmItem item)
        {
            Console.WriteLine("--- c# register --- {0} ---", key);

            Smh.wasmFuncs.Add(key, item);
        }
    }
}
