   M S B A M L   `   `   `  ����F  BAutoTubeWpf, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null5clr-namespace:AutoTubeWpf.ViewsAutoTubeWpf.Views  5      ?$clr-namespace:AutoTubeWpf.ViewModelsAutoTubeWpf.ViewModels  5      #    AutoTubeWpf.Views.AiShortView   5      W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation      5      R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      1localclr-namespace:AutoTubeWpf.Views     5      ;
viewmodels$clr-namespace:AutoTubeWpf.ViewModels     5      X TXceed.Wpf.Toolkit, Version=4.7.0.0, Culture=neutral, PublicKeyToken=3e4669d2f30244f44xctk)http://schemas.xceed.com/wpf/xaml/toolkit  5        � 	Resources  5       �� 
TargetType �� Property �� Value#  Xceed.Wpf.Toolkit.ColorPicker2   ,AutoTubeWpf.Views.AlignmentToStringConverter   AlignmentConverter��%  '��       '�� u     '�� �     '�� �    '  �    &	  �    �� 5   
   " ��6   .�5      ��  � ��$ Center=�62   �� 5       � ��$ 0,0,5,0q�6'   5      �� 5   
   " ��6   .�5      ��  � ��$ Center=�62   �� 5       � ��$ 0,0,5,0q�6'   5      �� 5   
   " ��6   .�5      ��  � ��$ Center=�62   �� 5       � ��$ 0,0,10,0q�6'   �� 5       � ��$	 120��6)   5      �� 5   
   " ��6   .�5      ��  � ��$ Center=�62   �� 5       � ��$ 0,0,5,0q�6'   5      �� 5!   
   "  6   .�5"      ��  � ��$ Center=�62   �� 5#       � ��$ 0,0,10,0q�6'   �� 5$       � ��$	 120��6&   �� 5%       �@ DisplayColorAndName$
 True��64   5&        5)   
   6+   5-      .��5/      � $��15q�6    � RowDefinitions 50   
   �� 51      $
��Auto �6   �� 52      $��* �6   �� 53      $
��Auto �6   �� 54      $
��Auto �6   .+�58   
   �� $��Inputs��6   $��0��6#   $��10q�60   $��0,0,0,10q�6=   .��59      �  5:      �� 5;      $
��Auto �6$   �� 5<      $
��Auto �6$    � ColumnDefinitions 5>      �� 5?      $
��Auto �6'   �� 5@      $��* �6'   �� 5A      $
��Auto �6'   .+�5E      �� $��0��6   $��0��6)   $��Background Video:��69   $��0,0,10,5q�6R   �� 5F      $��0��6   $��1��6'    �� 
IsReadOnly$
 True��6\   $��0,0,5,5q�6n   ��67   �� *BackgroundVideoPath+6n   �� 5G      $��0��6   $��2��6&   $��	Browse...��66   $��0,0,0,5q�6{   ��6J   �� *BrowseBackgroundVideoCommand+6{   �� 5J      $��1��6   $��0��6)   $��Output Folder:��69   $��0,5,10,0q�6O   �� 5K      $��1��6   $��1��6'   $
 True��6Y   $��0,5,5,0q�6k   ��67   �� *OutputFolderPath+6k   �� 5L      $��1��6   $��2��6&   $��	Browse...��66   $��0,5,0,0q�6x   ��6J   �� *BrowseOutputFolderCommand+6x   5M      5N      �� 5Q   
   $��Script��6   $��1��6#   $��10q�60   $��0,0,0,10q�6=   .��5R      �  5S      �� 5T      $
��Auto �6%   �� 5U      $��* �6%   .+�5Y      � $��0��6   $��0,0,0,10q�6%    5Z      �� 5[      $��* �6,   �� 5\      $
��Auto �6,   .+�5^      �� $��0��6   ��6/   �� *ScriptPrompt+ �� UpdateSourceTrigger$ PropertyChanged=��� 5`      -   5        $��1��5`      $��Generate Script��6.   {�6H   �� *CanGenerateScript+6p   5a      �� 5d      $��1��6    �� AcceptsReturn$
 True��5f      	 �� TextWrapping$
	 Wrap=�60   "
 �� VerticalScrollBarVisibility$

 Auto=�6D    �� 	IsEnabled$
 True��5g       � Language$ en-US�67   $	��150��6H   $��0,5,0,0q�6X   ��5e      �� *
ScriptText+$ PropertyChanged=�.��5g   i   .��6�   5h      5i      5j      �� 5m   
   $��Generation Options��6   $��2��6/   $��10q�6<   $��0,0,0,10q�6I   .��5n      �  5o      �� 5p      $
��Auto �6$   �� 5q      $
��Auto �6$   �� 5r      $
��Auto �6$   �� 5s      $
��Auto �6$    5u      �� 5v      $
��Auto �6'   �� 5w      $��* �6'   �� 5x      $
��Auto �6'   �� 5y      $��* �6'   �� 5z      $
��Auto �6'   �� 5{      $��* �6'   .+�5      �� $��0��6   $��0��6)   $��
TTS Voice:��69   $��0,0,5,5q�6K   �� 5�      $��0��6   $��1��6(   $��0,0,10,5q�6�   ��68   �� *AvailablePollyVoices+ �� SelectedItem 6e   �� *SelectedPollyVoice+6�   �� 5�      $��0��6   $��2��6)   $��
Font Size:��69   $��10,0,5,5q�6K   �� 5�      $��0��6   $��3��6'   $��60��6v   $��0,0,10,5q�6�   ��67   �� *
FontSize+$ PropertyChanged=�6�   �� 5�      $��0��6   $��4��6)   $��
Alignment:��69   $��10,0,5,5q�6K   �� 5�      $��0��6   $��5��6*    �� Orientation$ 
Horizontal=�6:   $��Center=�6S   $��0,0,0,5q�6n     5�       �� BasedOn��%�   '	�       7d��� 5�      " 	�6    8   .�5�      ��  � ��$ 0,0,5,0q�67   �� 5�       � ��$ 35��69   5�      5�      .+�5�      	� $
��Left��6"    d� 	IsChecked 5�   "   �� *SelectedSubtitleAlignment+ �� 	Converter# [   �� ConverterParameter$ Bottom Left��	� 5�      $��Center��6"    5�   "   �� *SelectedSubtitleAlignment+# [  $ Bottom Center��	� 5�      $��Right��6"    5�   "   �� *SelectedSubtitleAlignment+# [  $ Bottom Right��5�      �� 5�      $��1��6   $��0��6)   $��
Font Name:��69   $��0,5,5,5q�6K   �� 5�      $��1��6   $��1��6(   $��0,5,10,5q�5�      ��5�      �� *AvailableSubtitleFonts+ 5�      �� *SubtitleFontName+5�      �� 5�      $��1��6   $��2��6)   $��Font Color:��69   $��10,5,5,5q�6L     5�      $��1��6#   $��3��60   $��0,5,10,5q�5�   #      SelectedColor 5�   #   �� *SubtitleFontColor+5�   #   �� 5�      $��1��6   $��4��6)   $��Outline Color:��69   $��10,5,5,5q�6O     5�      $��1��6#   $��5��60   $��0,5,0,5q�5�   #    5�   #   �� *SubtitleOutlineColor+5�   #   �� 5�      $��2��6   $��0��6)   $��Outline Size:��69   $��0,5,5,5q�6N   �� 5�      $��2��6   $��1��6'   $��60��6�   $��0,5,10,5q�6�   ��67   �� *SubtitleOutlineThickness+$ PropertyChanged=�6�   �� 5�      $��2��6   $��2��6(   $��Background Box��68   $��10,5,5,5q�6�    6Q   �� *UseSubtitleBackgroundBox+6�   �� 5�      $��2��6   $��3��6)   $��	BG Color:��69   $��10,5,5,5q�6J   {�6\   �� *UseSubtitleBackgroundBox+  5�      $��2��6#   $��4��60   $��2��6@   $��0,5,0,5q�5�   #    5�   #   �� *SubtitleBackgroundColor+{�5�   #   �� *UseSubtitleBackgroundBox+5�   #   �� 5�      $��3��6   $��0��6(   $��2��68   $��Blur Background��6L   $��0,5,5,0q�6�    6f   �� *ApplyBackgroundBlur+6�   �� 5�      $��3��6   $��2��6(   $��2��68   $��Grayscale Background��6L   $��10,5,5,0q�6�    6k   �� *ApplyBackgroundGrayscale+6�   5�      5�      �� 5�   
   -   5        $��3��5�      $��Generate AI Short��6   $
��Left=�5�      $��0,10,0,0q�5�      ��5�      �� *GenerateAiShortAsyncCommand+{�5�      �� *CanGenerateAiShort+5�      5�      5�      