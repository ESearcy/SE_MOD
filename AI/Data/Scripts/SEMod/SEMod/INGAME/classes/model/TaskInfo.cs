using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEMod.INGAME.classes.model
{
    //////
    public class TaskInfo
    {
        private int maxResultsKept = 20;
        List<TaskResult> PreviousResults = new List<TaskResult>();
        public Action CallMethod;

        public TaskInfo(Action a)
        {
            CallMethod = a;
        }

        public void AddResult(TaskResult tr)
        {
            PreviousResults.Add(tr);
            while (PreviousResults.Count() > maxResultsKept)
                PreviousResults.RemoveAt(0);
        }
        public double GetAverageExecutionTime()
        {
            return PreviousResults.Average(a=>a.runtimeMs);
        }
        public double GetAverageCallCount()
        {
            return PreviousResults.Max(a => a.percentCapCall);
        }
        public double GetAverageCallDepth()
        {
            return PreviousResults.Max(a => a.percentCapDepth);
        }
    }

    public class TaskResult
    {
        public long runtimeMs;
        public long operationCount;
        public double percentCapDepth;
        public double percentCapCall;
        public TaskResult(long rm, long oc, double cc, double cd)
        {
            percentCapDepth = cd;
            percentCapCall = cc;
            runtimeMs = rm;
            operationCount = oc;
        }

        
    }

    //////
}
