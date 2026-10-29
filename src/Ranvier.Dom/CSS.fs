namespace Ranvier.Dom

/// <summary>CSS utilities shared by browser, native and server code.</summary>
module CSS =
    /// <summary>Serializes complete text as a CSS identifier using the CSSOM escaping profile.</summary>
    /// <param name="identifier">UTF-16 text; embedded null characters become U+FFFD, and a null reference becomes the text "null".</param>
    /// <remarks>Preserves non-ASCII code units, including unpaired surrogates, as the pinned engine does.</remarks>
    let escape (identifier: string) : string =
        let identifier = if isNull identifier then "null" else identifier
        let output = System.Text.StringBuilder ()

        for index in 0 .. identifier.Length - 1 do
            let character = identifier[index]
            let code = int character
            let digit = code >= 0x30 && code <= 0x39

            if code = 0 then
                output.Append (char 0xfffd) |> ignore
            elif
                code <= 0x1f
                || code = 0x7f
                || (digit
                    && (index = 0 || (index = 1 && identifier[0] = '-')))
            then
                output.Append('\\').Append(code.ToString ("x")).Append(' ')
                |> ignore
            elif character = '-' && identifier.Length = 1 then
                output.Append ("\\-") |> ignore
            elif
                code >= 0x80
                || character = '-'
                || character = '_'
                || digit
                || (code >= 0x41 && code <= 0x5a)
                || (code >= 0x61 && code <= 0x7a)
            then
                output.Append (character) |> ignore
            else
                output.Append('\\').Append(character) |> ignore

        output.ToString ()
