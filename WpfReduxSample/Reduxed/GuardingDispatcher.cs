using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace WpfReduxSample.Reduxed
{
    public class GuardingDispatcher : IActionDispatcher
    {
        private readonly HashSet<Type> ignoreList = new();
        private readonly IActionDispatcher dispatcher;
        private readonly ILogger logger;
        private bool dispatching = false;

        public GuardingDispatcher(IActionDispatcher dispatcher, ILogger logger)
        {
            this.dispatcher = dispatcher;
            this.logger = logger;
        }

        public void Ignore<T>()
        {
            ignoreList.Add(typeof(T));
        }

        public void Dispatch(object action)
        {
            if (action == null)
            {
                logger.LogWarning("Trying to dispatch null action.");
                return;
            }

            if (dispatching)
            {
                logger.LogWarning("Preventing recursive dispatch of action {0}.", action.GetType().ToString());
                return;
            }

            try
            {
                dispatching = true;

                var actionType = action.GetType();
                if (!ignoreList.Contains(actionType))
                {
                    logger.LogWarning("Action {0}", action.ToString());
                }

                dispatcher.Dispatch(action);
            }
            finally
            {
                dispatching = false;
            }
        }
    }
}
