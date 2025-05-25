using System.Diagnostics;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace IwacchiLab.PerfomanceTester
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class UdonSharpSpeedBenchmarkRunner : UdonSharpBehaviour
    {
        protected Stopwatch _stopwatch = new Stopwatch();

        private string _taskName;
        private int _count;
        protected int _iterations;
        private IUdonEventReceiver _onComplete;

        private bool _isExecute = false;

        public void RunBenchmark(string taskName, int count, int iterations, IUdonEventReceiver onComplete = null)
        {
            if (_isExecute)
            {
                return;
            }

            _isExecute = true;

            _taskName = taskName;
            _count = count;
            _onComplete = onComplete;
            _iterations = iterations;

            SendCustomEventDelayedFrames(nameof(_InitializeEvent), 0);
        }

        public void _InitializeEvent()
        {
            Initialize();
            SendCustomEventDelayedFrames(nameof(_RunTask), 0);
        }

        protected virtual void Initialize()
        {
        }

        public void _RunTask()
        {
            SendCustomEvent(_taskName);
            OnComplete();
        }


        protected void OnComplete()
        {
            if (Utilities.IsValid(_onComplete))
            {
                var udon = (UdonBehaviour)_onComplete;
                udon.SetProgramVariable($"__0_{"methodName"}__param", _taskName);
                udon.SetProgramVariable($"__0_{"count"}__param", _count);
                udon.SetProgramVariable($"__0_{"resultMilliseconds"}__param", _stopwatch.Elapsed.TotalMilliseconds);
                udon.SetProgramVariable($"__0_{"iterations"}__param", _iterations);
                udon.SendCustomEvent($"__0_{"CompleteTask"}");
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _onComplete = null;
        }
    }
}
