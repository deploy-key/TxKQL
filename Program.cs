using Newtonsoft.Json;
using Rx.Kql;
using System;
using System.Diagnostics;
using System.Reactive.Kql;
using System.Reactive.Kql.CustomTypes;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using Tx.Windows;

namespace ETWShenanigans
{
    internal class Program
    {
        public enum RxTypes
        {
            TDH_Transform_Dump,     // 0
            KQL_NodeHub_from_files, // 1
            KustoQuery_Observable,  // 2
            KustoQuery_Summarize,   // 3
            Raw_ETW                 // 4
        }

        static void Main(string[] args)
        {
            //logman create trace "DNS" -rt -ets -p "{1C95126E-7EEA-49A9-A3FE-A378B03DDB4D}"
            //logman create trace "TCP" -rt -ets -p "{7DD42A49-5329-4832-8DFD-43D979153A88}"

            // do you wan to run logman?
            if (false)
            {
                Process logman = Process.Start(
                    "logman.exe",
                    "create trace DNS -rt -ets -p \"{1C95126E-7EEA-49A9-A3FE-A378B03DDB4D}\"");
                logman.WaitForExit();
            }

            switch (RxTypes.KustoQuery_Summarize)
            {
                case RxTypes.TDH_Transform_Dump:
                    {
                        IObservable<IDictionary<string, object>> etw = EtwTdhObservable.FromSession("DNS");
                        var transformed = etw
                            .Select(e => new EtwEvent(e));

                        using (etw.Subscribe(e => handleEvent(e)))
                        {
                            Console.ReadLine();
                        }
                        break;
                    }
                case RxTypes.KQL_NodeHub_from_files:
                    {
                        IObservable<IDictionary<string, object>> etw = EtwTdhObservable.FromSession("DNS");
                        
                        KqlNodeHub hub = KqlNodeHub.FromFiles(etw, handleEvent, "etw", "query.kql");

                        // this clears the list of queries
                        // need to figure out how to target these better...
                        //hub._node.KqlQueryList.Clear();

                        using (etw.Subscribe(e => handleEvent(e)))
                        {
                            Console.ReadLine();
                        }
                        break;
                    }
                case RxTypes.KustoQuery_Observable:
                    {
                        IObservable<IDictionary<string, object>> etw = EtwTdhObservable.FromSession("DNS");

                        var kqlObservable = etw.KustoQuery("where Message !contains \"natepower\" | project EventId, Message");

                        kqlObservable.Subscribe(e => handleEvent(e));

                        var kqlObservable2 = etw.KustoQuery("where Message contains \"natepower\" | project EventId, Message");

                        // will alert in red!!!
                        using (kqlObservable2.Subscribe(e => handleAlert(e)))
                        {
                            Console.ReadLine();
                        }
                        break;
                    }
                case RxTypes.KustoQuery_Summarize:
                    {
                        IObservable<IDictionary<string, object>> etw = EtwTdhObservable.FromSession("TCP");

                        var kqlObservable = etw.KustoQuery("project EventData.daddr, now = now() | summarize make_set(EventData_daddr) by bin(now, 10s)");
                        //var kqlObservable = etw.KustoQuery("project daddr, now = now() | summarize make_set(EventData.daddr) by bin(now, 10s)");

                        using (kqlObservable.Subscribe(e => handleEvent(e)))
                        {
                            Console.ReadLine();
                        }
                        break;
                    }
                case RxTypes.Raw_ETW:
                    {
                        IObservable<EtwNativeEvent> session = EtwObservable.FromSession("TCP");
                        using (session.Subscribe(e => handleEvent(e)))
                        {
                            Console.ReadLine();
                        }

                        break;
                    }
                default:
                    {
                        break;
                    }
            }

            //logman stop "TCP" -ets
            //logman stop "DNS" -ets
        }

        static void handleEvent(EtwNativeEvent e)
        {
            Console.WriteLine("{0} {1}", e.TimeStamp, e.Id);
            Console.WriteLine(e.ReadAnsiString());
        }

        static void handleEvent(IDictionary<string,object> e)
        {
            Console.WriteLine(JsonConvert.SerializeObject(e));
        }

        static void handleAlert(IDictionary<string, object> e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(JsonConvert.SerializeObject(e));
            Console.ForegroundColor = ConsoleColor.White;
        }

        static void handleEvent(KqlOutput output)
        {
            Console.WriteLine(JsonConvert.SerializeObject(output.Output));
        }
    }
}