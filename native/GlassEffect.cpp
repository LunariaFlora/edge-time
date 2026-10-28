// Gaussian effect interop follows Microsoft Windows.UI.Composition-Win32-Samples.
// https://github.com/microsoft/Windows.UI.Composition-Win32-Samples
#include <windows.h>
#include <d2d1effects.h>
#include <windows.graphics.effects.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Effects.h>
#include <winrt/Windows.UI.Composition.h>
namespace ge=winrt::Windows::Graphics::Effects;
namespace abi=ABI::Windows::Graphics::Effects;
using Prop=ABI::Windows::Foundation::IPropertyValue;
struct Blur : winrt::implements<Blur,ge::IGraphicsEffect,ge::IGraphicsEffectSource,abi::IGraphicsEffectD2D1Interop> {
 ge::IGraphicsEffectSource source{nullptr};
 winrt::hstring Name(){return L"blur";} void Name(winrt::hstring const&){}
 HRESULT STDMETHODCALLTYPE GetEffectId(GUID* id) noexcept override {*id=CLSID_D2D1GaussianBlur;return S_OK;}
 HRESULT STDMETHODCALLTYPE GetNamedPropertyMapping(LPCWSTR name,UINT* index,abi::GRAPHICS_EFFECT_PROPERTY_MAPPING* mapping) noexcept override {if(wcscmp(name,L"BlurAmount"))return E_INVALIDARG;*index=0;*mapping=abi::GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT;return S_OK;}
 HRESULT STDMETHODCALLTYPE GetPropertyCount(UINT* count) noexcept override {*count=3;return S_OK;}
 HRESULT STDMETHODCALLTYPE GetProperty(UINT index,Prop** value) noexcept override try {using winrt::Windows::Foundation::PropertyValue;switch(index){case 0:*value=PropertyValue::CreateSingle(18.f).as<Prop>().detach();break;case 1:*value=PropertyValue::CreateUInt32(1).as<Prop>().detach();break;case 2:*value=PropertyValue::CreateUInt32(1).as<Prop>().detach();break;default:return E_BOUNDS;}return S_OK;}catch(...){return winrt::to_hresult();}
 HRESULT STDMETHODCALLTYPE GetSource(UINT index,abi::IGraphicsEffectSource** result) noexcept override {if(index)return E_BOUNDS;winrt::copy_to_abi(source,*reinterpret_cast<void**>(result));return S_OK;}
 HRESULT STDMETHODCALLTYPE GetSourceCount(UINT* count) noexcept override {*count=1;return S_OK;}
};
extern "C" __declspec(dllexport) HRESULT __stdcall CreateBlur(IInspectable* input, void** output) noexcept {try {winrt::Windows::Foundation::IInspectable inspected{nullptr};winrt::copy_from_abi(inspected,input);auto compositor=inspected.as<winrt::Windows::UI::Composition::Compositor>();auto effect=winrt::make_self<Blur>();effect->source=winrt::Windows::UI::Composition::CompositionEffectSourceParameter(L"background");auto factory=compositor.CreateEffectFactory(effect.as<ge::IGraphicsEffect>());auto brush=factory.CreateBrush();brush.SetSourceParameter(L"background",compositor.CreateBackdropBrush());*output=winrt::detach_abi(brush);return S_OK;}catch(...){return winrt::to_hresult();}}
