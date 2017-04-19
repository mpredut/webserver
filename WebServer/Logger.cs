/*
 * The Logger class used to debug and inform the user.
 **/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebServer
{

    public class Logger
    {

        public static readonly Logger Instance = null;

        public static void WriteLine(string message)
        {
            //   if(Instance == null)
            //Console.WriteLine(message);
        }

    }

}
