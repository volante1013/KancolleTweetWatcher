using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KancolleTweetWatcher
{
    public class FunctionJson
    {
		public string generatedBy { get; set; }
		public string configurationSource { get; set; }
		public Binding[] bindings { get; set; }
		public bool disabled { get; set; }
		public string scriptFile { get; set; }
		public string entryPoint { get; set; }
	}

	public class Binding
	{
		public string type { get; set; }
		public string schedule { get; set; }
		public bool useMonitor { get; set; }
		public bool runOnStartup { get; set; }
		public string name { get; set; }
	}
}
