
using System;           // DateTime
using System.IO;        // for writing log file


public class icLog
{
    protected bool _enabled = true;

    // log
    private TextWriter _logfile = null;

    // how many seconds to wait before logging a received event, 0: log everything 999: never log
    private int _log_interval = 0;
    private long _next_time = DateTime.Now.ToBinary();
    private string _filename = "";
	
	public void init(string name)
	{
		init(name, 0);
	}
    // init logging service, if name is "" then no logfile will be created
    public void init(string name, int interval)
    {
        if (_enabled == false)
            return;
        
        _log_interval = interval;
        int count = 1;

        // check whether to create logfile
        if (interval != 999 && name != "")
        {
            // create a valid log file
            while (true)
            {
                _filename = (name + count + ".log");

                if (!File.Exists(_filename))
                {
                    _logfile = new StreamWriter(_filename);
                    break;
                }
                else
                {
                    count++;
                }
            }
        }
    }

    public void close()
    {
        if (_enabled == false)
            return;
 
        if (_logfile != null)
        {
            // NOTE: calling Dispose or Close will cause program to generate exception
            //_logfile.Dispose();
            //_logfile.Close();
            _logfile = null;
        } 
    }

    ~icLog()
    {            
    }
	public void debug(string msg)
	{
		debug(msg,false);
	}
	
    public void debug(string msg, bool to_flush)
    {
        if (_enabled == false)
            return;

        if (_logfile != null)
        {
            bool to_log = true;

            if (_log_interval > 0)
            {
                // check if time's up to log
                long curr = DateTime.Now.ToBinary();
                if (curr >= _next_time)
                    _next_time = curr + (_log_interval * 10000000);
                else
                    to_log = false;
            }

            if (to_log)
            {
                // print
                _logfile.WriteLine(msg);
        
                if (to_flush)
                    _logfile.Flush();
            }
        }
		
		Console.WriteLine(msg);
    }

    public void flush()
    {
        if (_enabled == false)
            return;

        if (_logfile != null)
            _logfile.Flush();
    }

    // get a global singleton instance for log (note it's not thread-safe)
    static public icLog getInstance()
    {
        return _log;
    }

    //private StreamWriter _log = null;
    static private icLog _log = new icLog();
}
