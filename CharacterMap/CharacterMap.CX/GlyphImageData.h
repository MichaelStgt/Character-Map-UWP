#pragma once

#include "pch.h"
#include "Interop.h"
#include "CanvasTextLayoutAnalysis.h"
#include "DWriteFontSource.h"
#include <string>
#include "DWHelpers.h"
#include "SVGGeometrySink.h"
#include "PathData.h"
#include "Windows.h"

using namespace Microsoft::WRL;
using namespace CharacterMapCX;
using namespace Windows::Storage::Streams;
using namespace Platform::Collections;
using namespace Windows::Foundation::Numerics;
using namespace Windows::Foundation;

namespace CharacterMapCX
{
	public ref class GlyphImageData sealed
	{
	public:

		property IBuffer^ Buffer
		{
			IBuffer^ get() { return m_buffer; }
		}

		/// <summary>
		/// Top origin along the vertical central baseline.
		/// </summary>
		property float2 VerticalTopOrigin
		{
			float2 get() { return m_verticalTopOrigin; }
		}

		/// <summary>
		/// Bottom origin along vertical central baseline.
		/// </summary>
		property float2 VerticalBottomOrigin
		{
			float2 get() { return m_verticalBottomOrigin; }
		}

		/// <summary>
		/// Left origin along the horizontal Roman baseline.
		/// </summary>
		property float2 HorizontalLeftOrigin
		{
			float2 get() { return m_horizontalLeftOrigin; }
		}

		/// <summary>
		/// Right origin along the horizontal Roman baseline.
		/// </summary>
		property float2 HorizontalRightOrigin
		{
			float2 get() { return m_horizontalRightOrigin; }
		}

		property Rect ImageBounds
		{
			Rect get() { return m_imageBounds; }
		}

	internal:
		GlyphImageData(DWRITE_GLYPH_IMAGE_DATA data)
		{
			m_verticalTopOrigin = float2(data.verticalTopOrigin.x, data.verticalTopOrigin.y);
			m_verticalBottomOrigin = float2(data.verticalBottomOrigin.x, data.verticalBottomOrigin.y);

			m_horizontalLeftOrigin = float2(data.horizontalLeftOrigin.x, data.horizontalLeftOrigin.y);
			m_horizontalRightOrigin = float2(data.horizontalRightOrigin.x, data.horizontalRightOrigin.y);

			auto height = m_verticalBottomOrigin.y - m_verticalTopOrigin.y;
			auto width = m_horizontalRightOrigin.x - m_horizontalLeftOrigin.x;
			auto rect = new Windows::Foundation::Rect(m_horizontalLeftOrigin.x, m_verticalTopOrigin.y, width, height);

			auto b = (byte*)data.imageData;
			DataWriter^ writer = ref new DataWriter();
			writer->WriteBytes(Platform::ArrayReference<BYTE>(b, data.imageDataSize));
			m_buffer = writer->DetachBuffer();
			delete writer;
		}

	private:
		inline GlyphImageData() { }

		IBuffer^ m_buffer = nullptr;
		Rect m_imageBounds = Rect::Empty;
		float2 m_verticalTopOrigin = float2::zero();
		float2 m_verticalBottomOrigin = float2::zero();
		float2 m_horizontalLeftOrigin = float2::zero();
		float2 m_horizontalRightOrigin = float2::zero();
	};
}