using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FryProxy
{
    /// <summary>
    ///     Wraps single argument delegate
    /// </summary>
    /// <typeparam name="T">wrapped delegate argument type</typeparam>
    public class ActionWrapper<T>
    {
        /// <summary>
        ///     Wrapped delegate
        /// </summary>
        public Action<T> Action { get; set; }

        private void Invoke(T argument)
        {
            if (Action != null)
            {
                Action.Invoke(argument);
            }
        }
        
        /// <summary>
        ///     Combines 2 actions
        /// </summary>
        /// <param name="action"></param>
        /// <param name="wrapper"></param>
        /// <returns></returns>
        public static Action<T> operator + (Action<T> action, ActionWrapper<T> wrapper)
        {
            return action + (Action<T>) wrapper;
        }
    

        /// <summary>
        ///     Converts wrapper to action
        /// </summary>
        /// <param name="wrapper">wrapper instance</param>
        /// <returns>action</returns>
        public static implicit operator Action<T>(ActionWrapper<T> wrapper)
        {
            return wrapper.Invoke;
        }

    }
}
