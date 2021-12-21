// PEKAT VISION api
//
// A Python module for communication with PEKAT VISION 3.12.0 and higher
//
// Author: developers@pekatvision.com
// Date:   21 December 2021
// Web:    https://github.com/pekat-vision

namespace PekatVisionSDK {
    /// <summary>
    /// Result of image analysis.
    /// </summary>
    public class Result {
        /// <summary>
        /// Type of result.
        /// </summary>
        public ResultType ResultType { get; internal set; }
        /// <summary>
        /// Resulting image bytes, can be null.
        /// </summary>
        public byte[] Image { get; internal set; }
        /// <summary>
        /// Resulting context as string with JSON, can be null.
        /// </summary>
        public string Context { get; internal set; }
    }
}

