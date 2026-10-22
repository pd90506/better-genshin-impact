#!/usr/bin/env bash
# 从源码编译自包含的 libOpenCvSharpExtern（OpenCV 静态链接，不依赖 Homebrew 等外部库）。
#
# macOS 上没有官方的 Apple Silicon 版 OpenCvSharp 原生库，而 BetterGI 的地图追踪需要
# features2d（SIFT）/ calib3d / xfeatures2d 等模块，第三方精简版缺少这些模块，所以自行编译。
# 同一脚本也用于在 Linux 开发机上编译，以便运行单元测试。
#
# 用法：build-opencvsharp.sh <工作目录> <输出目录>
# 输出：<输出目录>/libOpenCvSharpExtern.dylib（macOS）或 libOpenCvSharpExtern.so（Linux）
set -euo pipefail

OPENCV_VERSION="${OPENCV_VERSION:-4.11.0}"
# 与 OpenCvSharp4 NuGet 包版本一致，保证原生导出函数与托管代码匹配
OPENCVSHARP_TAG="${OPENCVSHARP_TAG:-4.11.0.20250507}"

WORK_DIR="$(mkdir -p "$1" && cd "$1" && pwd)"
OUT_DIR="$(mkdir -p "$2" && cd "$2" && pwd)"
JOBS="${JOBS:-$( (command -v nproc >/dev/null && nproc) || sysctl -n hw.ncpu)}"

cd "$WORK_DIR"

fetch() {
  local repo="$1" ref="$2" dir="$3"
  if [ ! -d "$dir" ]; then
    git clone --quiet --depth 1 --branch "$ref" "https://github.com/$repo.git" "$dir"
  fi
}

fetch opencv/opencv "$OPENCV_VERSION" opencv
fetch opencv/opencv_contrib "$OPENCV_VERSION" opencv_contrib
fetch shimat/opencvsharp "$OPENCVSHARP_TAG" opencvsharp

PLATFORM_FLAGS=()
if [ "$(uname)" = "Darwin" ]; then
  PLATFORM_FLAGS+=(
    -DCMAKE_OSX_DEPLOYMENT_TARGET="${MACOSX_DEPLOYMENT_TARGET:-12.0}"
    -DWITH_OPENCL=OFF
    -DWITH_IPP=OFF
  )
else
  PLATFORM_FLAGS+=(
    -DWITH_GTK=OFF
    -DWITH_V4L=OFF
    -DCMAKE_POSITION_INDEPENDENT_CODE=ON
  )
fi

if [ ! -f "$WORK_DIR/opencv-install/lib/cmake/opencv4/OpenCVConfig.cmake" ] && [ ! -f "$WORK_DIR/opencv-install/lib64/cmake/opencv4/OpenCVConfig.cmake" ]; then
  cmake -S opencv -B opencv-build -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX="$WORK_DIR/opencv-install" \
    -DOPENCV_EXTRA_MODULES_PATH="$WORK_DIR/opencv_contrib/modules" \
    -DBUILD_SHARED_LIBS=OFF \
    -DOPENCV_ENABLE_NONFREE=ON \
    -DBUILD_TESTS=OFF -DBUILD_PERF_TESTS=OFF -DBUILD_DOCS=OFF -DBUILD_EXAMPLES=OFF -DBUILD_opencv_apps=OFF \
    -DBUILD_JAVA=OFF -DBUILD_opencv_java_bindings_generator=OFF \
    -DBUILD_opencv_python_bindings_generator=OFF -DBUILD_opencv_python_tests=OFF \
    -DBUILD_opencv_js=OFF -DBUILD_opencv_js_bindings_generator=OFF -DBUILD_opencv_objc_bindings_generator=OFF \
    -DBUILD_opencv_ts=OFF -DBUILD_opencv_gapi=OFF \
    -DBUILD_opencv_barcode=OFF -DBUILD_opencv_bioinspired=OFF -DBUILD_opencv_ccalib=OFF -DBUILD_opencv_datasets=OFF \
    -DBUILD_opencv_dnn_objdetect=OFF -DBUILD_opencv_dpm=OFF -DBUILD_opencv_fuzzy=OFF -DBUILD_opencv_hfs=OFF \
    -DBUILD_opencv_intensity_transform=OFF -DBUILD_opencv_mcc=OFF -DBUILD_opencv_rapid=OFF -DBUILD_opencv_reg=OFF \
    -DBUILD_opencv_rgbd=OFF -DBUILD_opencv_saliency=OFF -DBUILD_opencv_sfm=OFF -DBUILD_opencv_stereo=OFF \
    -DBUILD_opencv_structured_light=OFF -DBUILD_opencv_surface_matching=OFF -DBUILD_opencv_videostab=OFF \
    -DBUILD_opencv_xobjdetect=OFF -DBUILD_opencv_freetype=OFF -DBUILD_opencv_cvv=OFF -DBUILD_opencv_julia=OFF \
    -DBUILD_opencv_matlab=OFF -DBUILD_opencv_ovis=OFF -DBUILD_opencv_viz=OFF -DBUILD_opencv_signal=OFF \
    -DBUILD_ZLIB=ON -DBUILD_PNG=ON -DBUILD_JPEG=ON -DBUILD_TIFF=ON -DBUILD_WEBP=ON -DBUILD_OPENJPEG=ON \
    -DBUILD_PROTOBUF=ON -DWITH_PROTOBUF=ON \
    -DWITH_FFMPEG=OFF -DWITH_GSTREAMER=OFF -DWITH_OPENEXR=OFF -DWITH_TESSERACT=OFF -DWITH_EIGEN=OFF \
    -DWITH_ADE=OFF -DWITH_LAPACK=OFF -DWITH_OPENJPEG=ON -DWITH_JASPER=OFF -DWITH_QT=OFF -DWITH_VTK=OFF \
    -DWITH_1394=OFF -DWITH_OPENVINO=OFF -DWITH_TBB=OFF -DWITH_OPENMP=OFF \
    "${PLATFORM_FLAGS[@]}"
  cmake --build opencv-build --parallel "$JOBS"
  cmake --install opencv-build
fi

OPENCV_DIR="$WORK_DIR/opencv-install/lib/cmake/opencv4"
[ -d "$OPENCV_DIR" ] || OPENCV_DIR="$WORK_DIR/opencv-install/lib64/cmake/opencv4"

cmake -S opencvsharp/src -B opencvsharp-build -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DOpenCV_DIR="$OPENCV_DIR" \
  "${PLATFORM_FLAGS[@]}"
cmake --build opencvsharp-build --parallel "$JOBS"

if [ "$(uname)" = "Darwin" ]; then
  cp opencvsharp-build/OpenCvSharpExtern/libOpenCvSharpExtern.dylib "$OUT_DIR/"
  otool -L "$OUT_DIR/libOpenCvSharpExtern.dylib"
else
  cp opencvsharp-build/OpenCvSharpExtern/libOpenCvSharpExtern.so "$OUT_DIR/"
  ldd "$OUT_DIR/libOpenCvSharpExtern.so" || true
fi
echo "OpenCvSharpExtern 已输出到 $OUT_DIR"
