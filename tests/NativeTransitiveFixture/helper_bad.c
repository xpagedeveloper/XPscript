#if defined(_WIN32)
#define XPS_EXPORT __declspec(dllexport)
#else
#define XPS_EXPORT __attribute__((visibility("default")))
#endif

XPS_EXPORT int xps_native_helper_value(void)
{
    return 9999;
}
