cmake_minimum_required(VERSION 3.20)
project(xps_native_local C)

add_library(xps_native_local SHARED native_local.c)
set_target_properties(xps_native_local PROPERTIES
    OUTPUT_NAME "xps_native_local"
)
