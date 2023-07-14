using System.Diagnostics;

using Cave.DeviceControllers;
using Cave.DeviceControllers.Projectors.NEC;

namespace CaveTests
{
    internal class Program
    {
        static void ExceptionsTest()
        {
            ExceptionsTest1();
            ExceptionsTest2();
            ExceptionsTest3();
            ExceptionsTest4();
            ExceptionsTest5();
            ExceptionsTest6();
        }

        static void ExceptionsTest1()
        {
            string message = "My custom message instead of the default.";
            try
            {
                throw new NECCommandError((0, 0), message);
            }
            catch ( DeviceCommandError e )
            {
                // Should output a custom message instead of the default "The command cannot be recognized."
                Console.WriteLine(e.Message);
                Debug.Assert(e.Message.Equals(message));
                Console.WriteLine("Exceptions test 1 passed");
            }
        }

        static void ExceptionsTest2()
        {
            try
            {
                throw new NECCommandError((0, 1));
            }
            catch ( DeviceCommandError e )
            {
                // Should output the default message for error code (0, 1):
                // "The command is not supported by the model in use."
                Console.WriteLine(e.Message);
                Exception e2 = new NECCommandError((0, 1));
                Debug.Assert(e.Message.Equals("The command is not supported by the model in use."));
                Debug.Assert(e.Message.Equals(e2.Message));
                Console.WriteLine("Exceptions test 2 passed");
            }
        }

        static void ExceptionsTest3()
        {
            // "The command cannot be accepted because the power is off."
            NECCommandError cce1 = new((0x02, 0x0d));
            // "No signal"
            NECCommandError cce2 = new((0x02, 0x07));

            try
            {
                // Reassign a new error code to an exception that does not have a custom _message field set
                cce1.ErrorTuple = cce2.ErrorTuple;
                throw cce1;
            }
            catch( DeviceCommandError e )
            {
                // Should write "No signal" instead of "The command cannot be accepted because the power is off."
                Console.WriteLine(e.Message);
                Debug.Assert(e.Message.Equals("No signal"));
                Debug.Assert(e.Message.Equals(cce2.Message));
                Console.WriteLine("Exceptions test 3 passed.");
            }
        }

        static void ExceptionsTest4()
        {
            // Default message is "Memory in use"
            NECCommandError cce1 = new((2, 2), "A custom message goes here.");
            // "The specified input terminal is invalid."
            NECCommandError cce2 = new((1, 1));
            try
            {
                cce1.ErrorTuple = cce2.ErrorTuple;
                throw cce1;
            }
            catch( NECCommandError e )
            {
                // Should still write "A custom message goes here."
                Console.WriteLine(e.Message);
                Debug.Assert(e.Message.Equals("A custom message goes here."));
                Debug.Assert(!e.Message.Equals(cce2.Message));
                Debug.Assert(e.ErrorTuple == cce2.ErrorTuple);
                Console.WriteLine("Exceptions test 4 passed.");
            }
        }

        static void ExceptionsTest5()
        {
            NECCommandError cce1 = new((2, 2));
            try
            {
                throw cce1;
            }
            catch( Exception e )
            {
                Console.WriteLine(e.Message);
                Debug.Assert(e.Message.Equals("Memory in use"));
                if ( e is NECCommandError cce )
                {
                    Console.WriteLine($"cce.ErrorTuple == {cce.ErrorTuple}");
                    Console.WriteLine("Exceptions test 5 passed");
                }
            }
        }

        static void ExceptionsTest6()
        {
            string message = "A custom message for 2,2";
            NECCommandError cce1 = new((2, 2), message);
            try
            {
                throw cce1;
            }
            // Testing that we can catch it as a wider exception
            catch( Exception e )
            {
                Console.WriteLine(e.Message);
                Debug.Assert(e.Message.Equals("A custom message for 2,2"));
                Debug.Assert(e.Message.Equals(message));

                // Testing that we can use the is operator to determine its exact type(s)
                if ( e is DeviceCommandError dce )
                {
                    Console.WriteLine("Type is DeviceCommandError");
                    // 
                    if ( dce is NECCommandError cce )
                    {
                        Console.WriteLine("Type is ConcreteCommandError");
                        Console.WriteLine(cce.Message);
                        Debug.Assert(cce.Message.Equals(e.Message));
                        Console.WriteLine($"cce.ErrorTuple == {cce.ErrorTuple}");
                        Console.WriteLine("Exceptions test 6 passed");
                    }
                }
            }
        }


        static void Main( string[] args )
        {
            ExceptionsTest();
            Console.ReadKey();
        }
    }
}
