using System.Collections.Generic;

namespace ScryberHotReloader.Models {
    public class ScryberConfiguration {
        /// <summary>
        /// Page size (e.g., "A4", "Letter", "Legal")
        /// </summary>
        public string PageSize { get; set; } = "A4";

        /// <summary>
        /// Page orientation (Portrait or Landscape)
        /// </summary>
        public string PageOrientation { get; set; } = "Portrait";

        /// <summary>
        /// Custom font paths to load
        /// </summary>
        public List<string> FontPaths { get; set; } = new();

        /// <summary>
        /// Page width in points (custom size)
        /// </summary>
        public double? PageWidth { get; set; }

        /// <summary>
        /// Page height in points (custom size)
        /// </summary>
        public double? PageHeight { get; set; }

        /// <summary>
        /// Margin top in points
        /// </summary>
        public double MarginTop { get; set; } = 72; // 1 inch

        /// <summary>
        /// Margin bottom in points
        /// </summary>
        public double MarginBottom { get; set; } = 72;

        /// <summary>
        /// Margin left in points
        /// </summary>
        public double MarginLeft { get; set; } = 72;

        /// <summary>
        /// Margin right in points
        /// </summary>
        public double MarginRight { get; set; } = 72;
    }
}
