cmake_minimum_required(VERSION 3.20)
project(xps_native_transitive C)

add_library(xps_native_helper SHARED helper.c)
set_target_properties(xps_native_helper PROPERTIES OUTPUT_NAME "xps_native_helper")

add_library(xps_native_helper_bad SHARED helper_bad.c)
set_target_properties(xps_native_helper_bad PROPERTIES OUTPUT_NAME "xps_native_helper_bad")

add_library(xps_native_transitive SHARED main.c)
target_link_libraries(xps_native_transitive PRIVATE xps_native_helper)
set_target_properties(xps_native_transitive PROPERTIES OUTPUT_NAME "xps_native_transitive")

if(APPLE)
  set_target_properties(xps_native_helper PROPERTIES INSTALL_NAME_DIR "@rpath")
  set_target_properties(xps_native_transitive PROPERTIES
    BUILD_WITH_INSTALL_RPATH TRUE
    INSTALL_RPATH "@loader_path")
elseif(UNIX)
  set_target_properties(xps_native_transitive PROPERTIES
    BUILD_WITH_INSTALL_RPATH TRUE
    INSTALL_RPATH "$ORIGIN")
endif()
