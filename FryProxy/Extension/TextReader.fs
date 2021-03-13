namespace System.IO

module TextReader =
    
    let toSeq (reader: TextReader) =
        seq {
            let mutable line = null
            
            while (line <- reader.ReadLine(); line) <> null do
                yield line
        }
        



