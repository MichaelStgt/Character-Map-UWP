#include "pch.h"
#include "CanvasTextLayoutAnalysis.h"

using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI;

CharacterMapCX::CanvasTextLayoutAnalysis::CanvasTextLayoutAnalysis(ComPtr<ColorTextAnalyzer> analyzer, ComPtr<IDWriteFontFaceReference> fontFaceRef)
{
	m_hasColorGlyphs = analyzer->HasColorGlyphs;

	if (analyzer->IsCharacterAnalysisMode)
	{
		m_glyphLayerCount = analyzer->GlyphLayerCount;

		auto colors = ref new Array<Color>(analyzer->RunColors.size());
		float max = 255.0;
		for (unsigned int a = 0; a < analyzer->RunColors.size(); a = a + 1)
		{
			DWRITE_COLOR_F color = analyzer->RunColors[a];
			Color c = ColorHelper::FromArgb((UINT)(color.a * max), (UINT)(color.r * max), (UINT)(color.g * max), (UINT)(color.b * max));
			colors[a] = c;
		}
		m_colors = colors;

		auto gd = ref new Array<IVectorView<uint16>^>(analyzer->GlyphIndicies.size());

		for(unsigned int a = 0; a < analyzer->GlyphIndicies.size(); a = a + 1)
		{
			auto i = analyzer->GlyphIndicies[a];
			auto ind = ref new Vector<uint16>(sizeof(i));
			for (unsigned int b = 0; b < sizeof(i); b = b + 1)
			{
				ind->Append(i[b]);
			}

			gd[a] = ind->GetView();
		}

		m_indicies = gd;
	}

	for (GlyphImageFormat t : analyzer->GlyphFormats)
	{
		if (t == GlyphImageFormat::Png
			|| t == GlyphImageFormat::Jpeg
			|| t == GlyphImageFormat::Tiff
			|| t == GlyphImageFormat::PremultipliedB8G8R8A8)
		{
			m_containsBitmapGlyphs = true;
		}
		else if ((t & GlyphImageFormat::Colr) == GlyphImageFormat::Colr
			|| (t & GlyphImageFormat::Svg) == GlyphImageFormat::Svg)
		{
			m_containsVectorColorGlyphs = true;
		}

		if (m_containsBitmapGlyphs && m_containsVectorColorGlyphs)
			break;
	}

	auto vec = ref new Vector<GlyphImageFormat>(std::move(analyzer->GlyphFormats));
	m_glyphFormats = vec->GetView();

	// If we're analyzing the character only, we're done here.
	if (analyzer->IsCharacterAnalysisMode || fontFaceRef == nullptr)
		return;
	
	// Find out if we contain emoji
	/*ComPtr<IDWriteFontFace3> face;
	fontFaceRef->CreateFontFace(&face);
	uint32_t rangeCount;
	face->GetUnicodeRanges(0, nullptr, &rangeCount);
	DWRITE_UNICODE_RANGE* ranges = new (std::nothrow) DWRITE_UNICODE_RANGE[rangeCount];
	uint32* count;
	face->GetUnicodeRanges(UINT32_MAX, ranges, count);
	for (size_t i = 0; i < rangeCount; i++)
	{
		auto ranges = ranges[i];
	}*/

	// Get File Size
	m_fileSize = fontFaceRef->GetFileSize();

	// Attempt to get FilePath. 
	// This involves acquiring the FileLoader and querying it
	// for the file path;
	ComPtr<IDWriteFontFile> file;
	if (fontFaceRef->GetFontFile(&file) != S_OK)
		return;

	ComPtr<IDWriteFontFileLoader> loader;
	if (file->GetLoader(&loader) != S_OK)
		return;



	ComPtr<IDWriteLocalFontFileLoader> localLoader;
	if (loader->QueryInterface<IDWriteLocalFontFileLoader>(&localLoader) == S_OK)
	{
		const void* refKey = nullptr;
		uint32 size = 0;
		if (file->GetReferenceKey(&refKey, &size) == S_OK)
		{
			UINT filePathSize = 0;
			UINT filePathLength = 0;
			if (localLoader->GetFilePathLengthFromKey(refKey, size, &filePathLength) == S_OK)
			{
				wchar_t* buffer = new (std::nothrow) wchar_t[filePathLength + 1];
				if (localLoader->GetFilePathFromKey(refKey, size, buffer, filePathLength + 1) == S_OK)
				{
					m_filePath = ref new Platform::String(buffer);
				}
			}
		}
	}
}