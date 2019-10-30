﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Assets.Scripts.World
{
    /// <summary>
    /// This queue adds some small overhead although it provides interface much more convenient to use than manual tasks scheduling.
    /// It is very simplistic queue and adding tasks after execution start is not possible.
    /// </summary>
    class MultiThreadTaskQueue
    {
        readonly int _logicalProcessorCount = Environment.ProcessorCount;
        readonly List<Task> _pendingTasks = new List<Task>();
        bool _isRunning = false; // this queue is very simplistic and adding new tasks is impossible when the queue is executing tasks
        int _index = 0;

        /// <summary>
        /// Adds the given action to the queue. Tasks are not executed until RunAllInParallel method is called.
        /// Important: template types must match in type, order and number the parameters of the given method.
        /// </summary>
        public void ScheduleTask<T1, T2, T3>(Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3)
            where T1 : struct
            where T2 : struct
            where T3 : struct
        {
#if UNITY_EDITOR || UNITY_DEVELOPMENT
            Assertions(action.Method, 3);
#endif

            // use variable capture to 'pass in' parameters in order to avoid data share
            // we have to do it because values changed outside of a task are also changed in the task
            T1 copy1 = arg1;
            T2 copy2 = arg2;
            T3 copy3 = arg3;

            _pendingTasks.Add(new Task(() => action(copy1, copy2, copy3)));
        }

        /// <summary>
        /// Adds the given action to the queue. Tasks are not executed until RunAllInParallel method is called.
        /// Important: template types must match in type, order and number the parameters of the given method.
        /// </summary>
        public void ScheduleTask<T1, T2>(Action<T1, T2> action, T1 arg1, T2 arg2)
            where T1 : struct
            where T2 : struct
        {
#if UNITY_EDITOR || UNITY_DEVELOPMENT
            Assertions(action.Method, 2);
#endif

            // use variable capture to 'pass in' parameters in order to avoid data share
            // we have to do it because values changed outside of a task are also changed in the task
            T1 copy1 = arg1;
            T2 copy2 = arg2;

            _pendingTasks.Add(new Task(() => action(copy1, copy2)));
        }

        /// <summary>
        /// Adds the given action to the queue. Tasks are not executed until RunAllInParallel method is called.
        /// Important: template types must match in type, order and number the parameters of the given method.
        /// </summary>
        public void ScheduleTask<T>(Action<T> action, T arg)
            where T : struct
        {
#if UNITY_EDITOR || UNITY_DEVELOPMENT
            Assertions(action.Method, 1);
#endif

            // use variable capture to 'pass in' parameters in order to avoid data share
            // we have to do it because values changed outside of a task are also changed in the task
            T copy = arg;

            _pendingTasks.Add(new Task(() => action(copy)));
        }

        public void RunAllInParallel()
        {
            _isRunning = true;

            var _ongoingTasks = new Task[_logicalProcessorCount];

            // start first 8 (or any processors the target machine has)
            for (int i = 0; i < _logicalProcessorCount; i++)
            {
                if (_index == _pendingTasks.Count - 1) // less than 8 was scheduled
                    break;

                _ongoingTasks[i] = _pendingTasks[_index++];
                _ongoingTasks[i].Start();
            }

            // start new task as soon as we have a free thread available
            // and keep on doing that until you reach the end of the array
            do
            {
                int completedId = Task.WaitAny(_ongoingTasks);

                if (_index == _pendingTasks.Count - 1)
                    break;

                _ongoingTasks[completedId] = _pendingTasks[_index++];
                _ongoingTasks[completedId].Start();
            }
            while (true);

            Task.WaitAll(_ongoingTasks);

            _pendingTasks.Clear();
            _index = 0;
            _isRunning = false;
        }

#if UNITY_EDITOR || UNITY_DEVELOPMENT
        void Assertions(System.Reflection.MethodInfo info, int paramNumber)
        {
            if (info.GetParameters().Length != paramNumber)
                throw new System.ArgumentException($"Given action has the number of parameters different than {paramNumber}. " +
                    "Please use a method overload with the number of parameters corresponding to the number of parameters of the action.",
                    "action");
            else if (_isRunning)
                throw new System.ArgumentException("Adding new tasks after queue execution start in not allowed.");
        }
#endif
    }
}