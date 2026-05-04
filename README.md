
# Resource Encoder

A .NET Framework 4.8 tool for encoding and protecting resources inside assemblies using dnlib. This project is designed for advanced obfuscation workflows, including resource encryption, compression, and runtime decoding.

---

## Features

- Resource encoding and protection
- Integration with dnlib for assembly manipulation
- Compression and decompression support (LZMA-based)
- Mutation engine for obfuscation
- Runtime resource loader and manager
- Modular architecture for easy extension

---

## How It Works

1. Loads the target assembly.
2. Extracts and processes embedded resources.
3. Compresses and encodes resource data.
4. Injects runtime decoding logic into the assembly.
5. Rewrites the assembly with protected resources.

---

## Requirements

- .NET Framework 4.8
- Windows (recommended)

---

## Usage

```bash
ResourceEncoder.exe <input.exe>
```

Example:

```bash
ResourceEncoder.exe Target.exe
```

The tool will generate a protected version of the assembly in the same directory.

---

## Project Structure

```
Core/
 ├── Context.cs
 ├── DnlibUtils.cs
 ├── Inject Helper.cs
 ├── Logger.cs
 ├── Safe.cs
 └── Utils.cs

Mutation/
 ├── Mutation.cs
 └── MutationHelper.cs

Lazma/
 ├── Compress.cs
 └── Decompressor.cs

Resource Encoder/
 ├── Resource.cs
 ├── Resource manager.cs
 └── ResourceRuntime.cs

Program.cs
```

---

## Credits

This project uses:

- **dnlib**  
  https://github.com/0xd4d/dnlib

---

## Disclaimer

This tool is intended strictly for:

- Software protection research  
- Reverse engineering research  
- Educational purposes  



---

## Author

- https://github.com/Urbangfzero

---

