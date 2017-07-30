﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using ResourceManagementExamples.Interfaces;

namespace ResourceManagementExamples
{
    public class AutomaticManagerAspect : IInterceptor
    {
        private readonly IOpenClosable openClosable;

        private readonly TimeSpan maxiumumIdleTimeBeforeClosing;

        private readonly object lockObject = new object();

        private bool isOpen;

        private Stopwatch lastRequestStopwatch = new Stopwatch();

        private long requestNumber;

        public AutomaticManagerAspect(IOpenClosable openClosable, TimeSpan maxiumumIdleTimeBeforeClosing)
        {
            this.openClosable = openClosable;
            this.maxiumumIdleTimeBeforeClosing = maxiumumIdleTimeBeforeClosing;
        }

        public void Intercept(IInvocation invocation)
        {
            lock (lockObject)
            {
                if (!isOpen)
                {
                    openClosable.Open();

                    isOpen = true;
                }

                try
                {
                    invocation.Proceed();
                }
                finally
                {
                    lastRequestStopwatch.Restart();

                    requestNumber++;

                    if (requestNumber == 1)
                    {
                        ScheduleResourceClosing();
                    }
                }
            }
        }

        private async void ScheduleResourceClosing()
        {
            var delay = maxiumumIdleTimeBeforeClosing;

            while (true)
            {
                await Task.Delay(delay).ConfigureAwait(false);

                lock (lockObject)
                {
                    delay = maxiumumIdleTimeBeforeClosing - lastRequestStopwatch.Elapsed;

                    if (requestNumber == 1 || delay <= TimeSpan.Zero)
                    {
                        requestNumber = 0;

                        isOpen = false;

                        lastRequestStopwatch.Reset();

                        openClosable.Close();

                        return;
                    }
                }
            }
        }
    }
}