#!/bin/bash
# Quick setup and test script for the pipeline

set -e

PROJECT_DIR="/Users/mesely/ses_yonu_test_2d"
cd "$PROJECT_DIR"

echo "🚀 Pipeline Quick Setup & Test"
echo "=============================="
echo ""

# Check Python
if ! command -v python3 &> /dev/null; then
    echo "❌ Python3 not found. Install from python.org"
    exit 1
fi
echo "✅ Python3: $(python3 --version)"

# Create venv if needed
if [ ! -d "venv" ]; then
    echo "📦 Creating virtual environment..."
    python3 -m venv venv
fi

source venv/bin/activate
echo "✅ Virtual environment activated"

# Install dependencies
echo "📥 Installing dependencies..."
pip install -q numpy matplotlib sounddevice 2>/dev/null || pip install numpy matplotlib sounddevice

# Optional: faster-whisper, tensorflow (takes longer)
echo "📥 Optional packages (Whisper/TensorFlow)..."
pip install faster-whisper -q 2>/dev/null || echo "   ⚠️  Whisper skipped (optional)"
pip install tensorflow -q 2>/dev/null || echo "   ⚠️  TensorFlow skipped (optional)"

echo ""
echo "✅ Setup complete!"
echo ""
echo "Next steps:"
echo "1. Run the simple pipeline:"
echo "   python3 SimplePipeline.py"
echo ""
echo "2. Or run the full pipeline (with Unity support):"
echo "   python3 RealTimeSPLVisualizer.py"
echo ""
echo "📖 See SETUP.md for detailed instructions."
