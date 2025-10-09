class Kustoterminal < Formula
  desc "Interactive terminal for Azure Data Explorer (Kusto) with syntax highlighting and auto-completion"
  homepage "https://github.com/basaba/kusto-terminal"
  version "0.1.0"
  license "MIT"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/basaba/kusto-terminal/releases/download/v0.1.0/KustoTerminal-osx-arm64.tar.gz"
      sha256 "placeholder"
    else
      url "https://github.com/basaba/kusto-terminal/releases/download/v0.1.0/KustoTerminal-osx-x64.tar.gz"
      sha256 "placeholder"
    end
  end

  depends_on :macos

  def install
    bin.install "KustoTerminal" => "kustoterminal"
    
    # Install any additional files if needed
    if File.exist?("config.json")
      (etc/"kustoterminal").install "config.json"
    end
  end

  def caveats
    <<~EOS
      KustoTerminal has been installed!
      
      To get started, run:
        kustoterminal
      
      For more information, visit:
        https://github.com/basaba/kusto-terminal
    EOS
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/kustoterminal --version 2>&1", 0)
  end
end
