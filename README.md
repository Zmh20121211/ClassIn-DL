<h4 align="right">
  <a href="README_zh.md">简体中文</a> | English
</h4>

<!-- PROJECT LOGO -->
<div align="center">
  <br />
  
  <h1 style="font-size: 3.5rem; margin-bottom: 0.5rem;">
    <span style="color: #2E86C1;">ClassIn</span> Video Downloader
  </h1>

[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![Release][release-shield]][release-url]
[![Downloads][downloads-shield]][release-url]

<h4>
  <a href="https://github.com/ZMH21306/ClassIn-DL/issues/new?template=bug_report.yml">Report Bug</a>
 ·    
  <a href="https://github.com/ZMH21306/ClassIn-DL/issues/new?template=feature_request.yml">Request Feature</a>
</h4>

</div>

<!-- ABOUT THE PROJECT -->
## About The Project

A powerful ClassIn video downloader with both graphical and command-line versions, designed to help users easily download ClassIn course videos 📹

Features high-speed downloads, batch processing, and a user-friendly WPF interface built with C# 🚀

Supports parsing video links from ClassIn platform and managing download tasks efficiently ⚡

Requires HTTP Debugger Pro for capturing video requests, providing a reliable way to obtain video resources 🔍

> **⚠️ Important Notice**
>
> This tool is a technical learning project. According to the ClassIn platform rules, student accounts typically only have the **right to view** course replays, **not to download** them. Unauthorized downloading of course content may **infringe intellectual property rights and violate the platform's user agreement**. Please only use this tool to download course content for which you have legal rights, and assume all related risks.

<!-- COMPATIBILITY -->
## Compatibility

|    Platform    | Required Version |  Architectures   | Compatible |
|:--------------:|:----------------:|:----------------:|:----------:|
| 🪟 **Windows** |     `7 SP1+`     | `x86_64`/`x86`/`arm64` |     ✅      |
|  🐧 **Linux**  |  `glibc 2.35+`   | `x86_64`/`arm64` |     ❌      |
|  🍎 **macOS**  |     `11.0+`      | `x86_64`/`arm64` |     ❌      |

<!-- ENCODING ISSUE -->
## Encoding Issue Notice

> **🔧 HTTP Debugger Pro Encoding Problem**
>
> Currently, some users encounter **garbled text** in the output when using HTTP Debugger Pro to capture requests. This is caused by the ClassIn server using different character encodings in responses.
>
> **Temporary Solution**: In some cases, converting the captured text from **GBK encoding to UTF-8 encoding** can fix part of the garbled text issues.
>
> **Development Plan**: We are developing an **automatic encoding repair feature** and plan to integrate GBK→UTF-8 automatic conversion in future releases to address this issue. This feature is currently under active development.
>
> If you encounter this issue, you can try manually converting the encoding using external tools (such as Notepad++, VSCode, etc.), or stay tuned for our updates.

<!-- ROADMAP -->
## Roadmap

### ✅ Completed Features
- ✅ Graphical user interface (WPF)
- ✅ Command-line interface support
- ✅ Basic ClassIn video download functionality
- ✅ Batch video downloading
- ✅ Video link parsing from HTTP requests
- ✅ Multi-threaded downloads with configurable thread count
- ✅ Real-time download speed display
- ✅ Download progress tracking
- ✅ Error handling and logging
- ✅ Configurable download directory
- ✅ Adjustable concurrent download limit

### 🔄 Planned Features
- 🔄 Self-service packet capture (long-term)
- 🔄 **Automatic encoding repair (GBK→UTF-8)**

Visit [GitHub Issues](https://github.com/ZMH21306/ClassIn-DL/issues) to see all requested features (and known issues).

<!-- TUTORIAL VIDEO -->
## Tutorial Video

<!-- Video link to be added later -->

<!-- DOWNLOAD LINKS -->
## Download

> [!TIP]
> For the best compatibility, please use the latest version of the tool.

Get the latest version of ClassIn Video Downloader for Windows:

| Platform | Architecture | Download Link |
|:--------:|:------------:|:-------------:|
| Windows  | x86_64       | [GitHub Direct](https://github.com/ZMH21306/ClassIn-DL/releases/download/v1.0.0/Classin_DL-v1.0.0-Windows-x64.exe) <br> [CDN Mirror](https://gh-proxy.org/https://github.com/ZMH21306/Classin-DL/releases/download/v1.0.0/Classin_DL-v1.0.0-Windows-x64.exe) |
| Windows  | x86          | [GitHub Direct](https://github.com/ZMH21306/ClassIn-DL/releases/download/v1.0.0/Classin_DL-v1.0.0-Windows-x86.exe) <br> [CDN Mirror](https://gh-proxy.org/https://github.com/ZMH21306/Classin-DL/releases/download/v1.0.0/Classin_DL-v1.0.0-Windows-x86.exe) |
| Windows  | arm64        | [GitHub Direct](https://github.com/ZMH21306/ClassIn-DL/releases/download/v1.0.0/Classin_DL-v1.0.0-Windows-arm64.exe) <br> [CDN Mirror](https://gh-proxy.org/https://github.com/ZMH21306/Classin-DL/releases/download/v1.0.0/Classin_DL-v1.0.0-Windows-arm64.exe) |

<!-- CONTRIBUTING -->
## Contributing

Contributions make the open source community an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion, fork the repo and create a pull request. You can also simply open an issue with the "Enhancement" tag. Don't forget to give the project a star⭐! Thanks again!

1. Fork the Project
2. Create your Feature Branch (git checkout -b feature/AmazingFeature)
3. Commit your Changes (git commit -m 'Add some AmazingFeature')
4. Push to the Branch (git push origin feature/AmazingFeature)
5. Open a Pull Request

Thanks to all contributors who have participated in this project!

<a href="https://github.com/ZMH21306/ClassIn-DL/graphs/contributors"><img src="http://contrib.nn.ci/api?repo=ZMH21306/ClassIn-DL" alt="Contributors" /></a>

<!-- LICENSE -->
## License

Distributed under the GPL v3.0 License. See `LICENSE` for more information.

Copyright © 2025 ZMH.

<!-- CONTACT -->
## Contact

* [E-mail](mailto:2130606191@qq.com) - 2130606191@qq.com
* [QQ Group](https://qm.qq.com/q/PlUBdzqZCm) - 2130606191

## Acknowledgments

* Special thanks to all open source projects that made this tool possible!

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=ZMH21306/ClassIn-DL&type=timeline&legend=top-left)](https://www.star-history.com/#ZMH21306/ClassIn-DL&type=timeline&legend=top-left)

<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->
[forks-shield]: https://img.shields.io/github/forks/ZMH21306/ClassIn-DL.svg?style=for-the-badge
[forks-url]: https://github.com/ZMH21306/ClassIn-DL/network/members
[stars-shield]: https://img.shields.io/github/stars/ZMH21306/ClassIn-DL.svg?style=for-the-badge
[stars-url]: https://github.com/ZMH21306/ClassIn-DL/stargazers
[issues-shield]: https://img.shields.io/github/issues/ZMH21306/ClassIn-DL.svg?style=for-the-badge
[issues-url]: https://github.com/ZMH21306/ClassIn-DL/issues
[release-shield]: https://img.shields.io/github/v/release/ZMH21306/ClassIn-DL?style=for-the-badge
[release-url]: https://github.com/ZMH21306/ClassIn-DL/releases/latest
[downloads-shield]: https://img.shields.io/github/downloads/ZMH21306/ClassIn-DL/total?style=for-the-badge
[qqgroup-shield]: https://img.shields.io/badge/QQ_Group-2130606191-blue.svg?color=blue&style=for-the-badge
[qqgroup-url]: https://qm.qq.com/q/4w0AZhrAcU
