cmake_minimum_required(VERSION 3.16)
project(OdbcMemoryBenchmark LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(OdbcMemoryBenchmark OdbcMemoryBenchmark.cpp)

# Link ODBC and Windows memory APIs
target_link_libraries(OdbcMemoryBenchmark PRIVATE odbc32 psapi)
