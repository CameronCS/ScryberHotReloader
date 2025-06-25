using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScryberHotReloader
{
    class Defaults
    {
        public const string DefaultHtml = @" <!-- Default HTML for Startup -->
<!DOCTYPE HTML >
<html lang='en' xmlns='http://www.w3.org/1999/xhtml' >
    <head>
        <title>Hello World</title>
    </head>
    <body>
        <p>Hello from {{ model.AppName }}</p>
    </body>
</html>
";

        public const string DefaultCS = @"// Default C# for Startup
class Model {
    public string AppName = ""Scryber Hot Reloader"";
}
";
    }
}
