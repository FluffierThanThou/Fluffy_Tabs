﻿// PawnTable_PawnTableOnGUI.cs
// Copyright Karel Kroeze, 2018-2018

using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkTab
{
    [HarmonyPatch( typeof( PawnTable ), "PawnTableOnGUI" )]
    public class PawnTable_PawnTableOnGUI
    {
        private static Type ptt = typeof( PawnTable );
        private static MethodInfo RecacheIfDirtyMethod = AccessTools.Method( ptt, "RecacheIfDirty" );
        private static FieldInfo cachedColumnWidthsField = AccessTools.Field( ptt, "cachedColumnWidths" );
        private static FieldInfo scrollPositionField = AccessTools.Field( ptt, "scrollPosition" );
        private static FieldInfo cachedRowHeightsField = AccessTools.Field( ptt, "cachedRowHeights" );
        private static FieldInfo standardMarginField = AccessTools.Field( typeof( Window ), "StandardMargin" );

        static PawnTable_PawnTableOnGUI()
        {
            if ( RecacheIfDirtyMethod == null ) throw new NullReferenceException( "RecacheIfDirty field not found." );
            if ( cachedColumnWidthsField == null ) throw new NullReferenceException( "cachedColumnWidths field not found." );
            if ( scrollPositionField == null ) throw new NullReferenceException( "scrollPosition field not found." );
            if ( cachedRowHeightsField == null ) throw new NullReferenceException( "cachedRowHeights field not found." );
            if ( standardMarginField == null ) throw new NullReferenceException( "standardMargin field not found." );
        }

        public static bool Prefix( PawnTable __instance, Vector2 position )
        {
            if (Event.current.type == EventType.Layout)
            {
                return false;
            }

            RecacheIfDirtyMethod.Invoke(__instance, null);

            // get fields
            var cachedSize = __instance.Size;
            var columns = __instance.ColumnsListForReading;
            var cachedColumnWidths = cachedColumnWidthsField.GetValue( __instance ) as List<float>;
            var cachedHeaderHeight = __instance.HeaderHeight;
            var cachedHeightNoScrollbar = __instance.HeightNoScrollbar;
            var scrollPosition = (Vector2) scrollPositionField.GetValue( __instance );
            var headerScrollPosition = new Vector2(scrollPosition.x, 0f);
            var cachedPawns = __instance.PawnsListForReading;
            var cachedRowHeights = cachedRowHeightsField.GetValue( __instance ) as List<float>;
            var standardWindowMargin = (float)standardMarginField.GetRawConstantValue();

            // this is the main change, vanilla hardcodes both outRect and viewRect to the cached size.
            // Instead, we want to limit outRect to the available view area, so a horizontal scrollbar can appear.
            var outWidth = Mathf.Min(cachedSize.x, UI.screenWidth - standardWindowMargin * 2f);
            var viewWidth = cachedSize.x - 16f;

            Rect tableOutRect = new Rect(
                position.x,
                position.y + cachedHeaderHeight,
                outWidth,
                cachedSize.y - cachedHeaderHeight);
            Rect tableViewRect = new Rect(
                0f,
                0f,
                viewWidth,
                cachedHeightNoScrollbar - cachedHeaderHeight);
            
            Rect headerOutRect = new Rect(
                position.x,
                position.y,
                outWidth,
                cachedHeaderHeight );
            Rect headerViewRect = new Rect(
                0f,
                0f,
                viewWidth,
                cachedHeaderHeight );

            // increase height of table to accomodate scrollbar if necessary and possible.
            if (viewWidth > outWidth && (cachedSize.y + 16f) < UI.screenHeight) // NOTE: this is probably optimistic about the available height, but it appears to be what vanilla uses.
                tableOutRect.height += 16f;

            // we need to add a scroll area to the column headers to make sure they stay in sync with the rest of the table
            Widgets.BeginScrollView( headerOutRect, ref headerScrollPosition, headerViewRect, false );
            int x_pos = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                int colWidth;
                if (i == columns.Count - 1)
                {
                    colWidth = (int)(viewWidth - x_pos);
                }
                else
                {
                    colWidth = (int)cachedColumnWidths[i];
                }
                Rect rect = new Rect(x_pos, 0f, colWidth, (int)cachedHeaderHeight);
                columns[i].Worker.DoHeader( rect, __instance );
                x_pos += colWidth;
            }
            Widgets.EndScrollView();


            Widgets.BeginScrollView(tableOutRect, ref scrollPosition, tableViewRect );
            scrollPositionField.SetValue( __instance, scrollPosition );
            int y_pos = 0;
            for (int j = 0; j < cachedPawns.Count; j++)
            {
                x_pos = 0;
                if ((float)y_pos - scrollPosition.y + (int)cachedRowHeights[j] >= 0f && (float)y_pos - scrollPosition.y <= tableOutRect.height)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.2f);
                    Widgets.DrawLineHorizontal(0f, y_pos, tableViewRect.width);
                    GUI.color = Color.white;
                    Rect rowRect = new Rect(0f, y_pos, tableViewRect.width, (int)cachedRowHeights[j]);
                    if (Mouse.IsOver(rowRect))
                    {
                        GUI.DrawTexture(rowRect, TexUI.HighlightTex);
                    }
                    for (int k = 0; k < columns.Count; k++)
                    {
                        int cellWidth;
                        if (k == columns.Count - 1)
                        {
                            cellWidth = (int)(viewWidth - x_pos);
                        }
                        else
                        {
                            cellWidth = (int)cachedColumnWidths[k];
                        }
                        Rect rect3 = new Rect(x_pos, y_pos, cellWidth, (int)cachedRowHeights[j]);
                        columns[k].Worker.DoCell( rect3, cachedPawns[j], __instance );
                        x_pos += cellWidth;
                    }
                    if (cachedPawns[j].Downed)
                    {
                        GUI.color = new Color(1f, 0f, 0f, 0.5f);
                        Widgets.DrawLineHorizontal(0f, rowRect.center.y, tableViewRect.width);
                        GUI.color = Color.white;
                    }
                }
                y_pos += (int)cachedRowHeights[j];
            }
            Widgets.EndScrollView();

            return false;
        }
    }
}