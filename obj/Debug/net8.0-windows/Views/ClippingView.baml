   M S B A M L   `   `   `  ����F  BAutoTubeWpf, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null5clr-namespace:AutoTubeWpf.ViewsAutoTubeWpf.Views  5      ?$clr-namespace:AutoTubeWpf.ViewModelsAutoTubeWpf.ViewModels  5      $    AutoTubeWpf.Views.ClippingView   5      W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation      5      R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      1localclr-namespace:AutoTubeWpf.Views     5      ;
viewmodels$clr-namespace:AutoTubeWpf.ViewModels     5      -   5          J� 	AllowDrop$
  True��5      .��5      � $��15q�6
    � RowDefinitions 5   
   �� 5      $
��Auto �6   �� 5      $��* �6   �� 5      $
��Auto �6   �� 5      $
��Auto �6   �� 5      $
��Auto �6   .+�5   
   �� $��Input Video(s)��6   $��0��6+   $��10q�68   $��0,0,0,10q�6E   .��5      �  � ColumnDefinitions 5      �� 5      $��* �6'   �� 5      $
��Auto �6'   �� 5      $
��Auto �6'   .+�5      �� $��0��6    �� 
IsReadOnly$
 True��6L   $��Center=�6^   $��0,0,10,0q�6�   ��6*   �� *InputDisplayPath+6�   �� 5       $��1��6   $��Batch (Folder)��6+   $��Center=�6f   $��0,0,10,0q�6�    d� 	IsChecked 6D   �� *IsBatchMode+6�   �� 5!      $��2��6   $��	Browse...��6)   ��6=   �� *SelectInputCommand+5"      5#      �� 5&   
   $��Video Queue��6   $��1��6(   $��10q�65   $��0,0,0,10q�6B   .��5'      &   System.Windows.Controls.DataGrid  -   5           AutoGenerateColumns$ False��5)         CanUserAddRows$ False��64      
IsReadOnly$
 True��5*         SelectionMode$ Single=�6*   ��5'      �� *
VideoQueue+	 �� SelectedItem	 5(      �� *SelectedVideoItem+
 �� Mode$
 TwoWay=�   Columns 5-      0  *System.Windows.Controls.DataGridTextColumn  5.      ,  &System.Windows.Controls.DataGridColumn   Header$ 	File Name��6*      Width5  /System.Windows.Controls.DataGridLengthConverter$ * 6Z   1  +System.Windows.Controls.DataGridBoundColumn   Binding 6=   �� *
FileName+6Z     5/      $ Status��6*   $
 Auto 6U    6:   �� *Status+6U     50      $ Duration��6*   $
 Auto 6z    6<   �� *
Duration+ �� StringFormat$ {0:hh\:mm\:ss}��6z   52      53      �� 56   
   $��Preview��6   $��2��6$   $��10q�61   $��0,0,0,10q�6>   y�58      �� 59       �� Path$ SelectedVideoItem�6     �� 	Converter 5:      1   +AutoTubeWpf.Views.NullToVisibilityConverter  5;       5=      .��5?      �  5@      �� 5A      $��* �6$   �� 5B      $
��Auto �6$   .+�5E      �� $��0��6   
���   � �� CornerRadius$ 3��69   $��0,0,0,5q�6J   ._�5F      r� -   5         r�Name$ PreviewPlayer��5F   #   $	��200��6:   $	��300��6J    r� LoadedBehavior$ Manual=�5H   #    r� UnloadedBehavior$
 Stop=�6;    r� Stretch$ Uniform=�6S    r� ScrubbingEnabled$
 True��5I   #    r� Source 5G   #   �� *PlayerSource+5O      5P      �� 5S      $��1��6    �� Orientation$ Vertical=�6*   $��0,5,0,0q�6A   .+�5T      �� -   5         ��Name$ 
SeekSlider��5T       � Minimum$ 0Q�62   $��0,0,0,5q�5V       � Maximum 5T   >   �� *PlayerDurationSeconds+ �� FallbackValue$ 1�� � Value 5U      �� *PlayerPositionSeconds+$
 TwoWay=�5\      �� 5]      $��Right=�6I   $��0,-5,0,5q�6e   ��6    �� *PlayerPositionFormatted+6e   �� 5_      $ 
Horizontal=�6    .+�5`      �� $��5q�6F   $��0,0,10,0q�6R    � ToolTip$ 
Play/Pause��6d   ��6!   �� *PlayPauseCommand+��5a      �� 5b   "   $ IsPlayerPlaying�6*    5c   &   4   .AutoTubeWpf.Views.BoolToPlayPauseTextConverter  5d   *   5f   #   5h      �� 5j      $��5q�6A   $��0,0,20,0q�6M   $
 Stop��6_   $
��Stop��6n   ��6!   �� *StopCommand+6n   �� 5m      $��Speed:��6$   $��Center=�62   $��0,0,5,0q�6M   �� 5n      $	 0.1Q�6!   $	 2.0Q�6/   $	��100��6m     �� TickFrequency$	  0.1Q�6y   ! �� IsSnapToTickEnabled$
! True��6�   $��Center=�6�    6=   �� *PlayerSpeedRatio+$
 TwoWay=�6�   �� 5o      $��Center=�6^   $��5,0,0,0q�6y   ��6$   �� *PlayerSpeedRatio+$ {0:F1}x��6y   5p      5q      5r      5s      �� 5v   
   $��Clipping Options��6   $��3��6-   $��10q�6:   $��0,0,0,10q�6G   .��5w      �  5x      �� 5y      $��* �6'   �� 5z      $��* �6'    5|      �� 5}      $
��Auto �6$   �� 5~      $
��Auto �6$   �� 5      $
��Auto �6$   �� 5�      $
��Auto �6$   .+�5�      �� $��0��6   $��0��6*   $��	0,0,10,10q�6:   .+�5�      �� $$��Clip Duration Range (seconds):��6    $��0,0,0,5q�6F   �� 5�      $ 
Horizontal=�6!   .+�5�      �� $
��Min:��6%   $��Center=�61   $��0,0,5,0q�6L   �� 5�      $��60��6g   ��6#   �� *MinClipLength+" �� UpdateSourceTrigger$" PropertyChanged=�6g   �� 5�      $
��Max:��6%   $��Center=�61   $��10,0,5,0q�6L   �� 5�      $��60��6g   ��6#   �� *MaxClipLength+$" PropertyChanged=�6g   5�      5�      �� 5�      $��1��6   $��0��6+   $��	0,0,10,10q�6;   .+�5�      �� $��Max Clips per Video:��6!   $��0,0,0,5q�6=   �� 5�      $��80��6_   $
��Left=�6j   ��6   �� *	ClipCount+$" PropertyChanged=�6j   5�      �� 5�      $��0��6   $��1��6*   $��	10,0,0,10q�6:   .+�5�      �� $��Use Scene Detection��6   $��0,0,0,5q�6e    6=   �� *UseSceneDetection+6e   �� 5�      $��Scene Threshold (0-100):��6    $��0,0,0,5q�6h   {�6@   �� *UseSceneDetection+6h   �� 5�      $��80��6c   $
��Left=�6n   ��6   �� *SceneThreshold+$" PropertyChanged=�{�6�   �� *UseSceneDetection+5�      �� 5�      $��1��6   $��1��6+   $��	10,0,0,10q�6;   .+�5�      �� $%��Format Clips as Vertical (9:16)��6     6J   �� *FormatAsVertical+5�      �� 5�      $��2��6   $��1��6+   $ Vertical=�6;   $��10,5,0,0q�6R   .+�5�      �� $��Remove Audio��6    $��0,0,0,5q�6Y    67   �� *RemoveAudio+6Y   �� 5�      $$��Mirror Video (Horizontal Flip)��6    $��0,0,0,5q�6k    6I   �� *MirrorVideo+6k   �� 5�      $/��)Enhance Video (Basic Contrast/Saturation)��6     6T   �� *EnhanceVideo+5�      5�      5�      �� 5�   
   $��4��6   $ 
Horizontal=�6"   $
��Left=�6;   .+�5�      �� $��Clear Queue��6   $��0,0,10,0q�6Q   ��6+   �� *ClearQueueCommand+6Q   �� 5�      ��6   �� *StartStopClippingCommand+��5�      �� 5�      $ IsProcessing�6"   $ Start Clipping Queue��66    5�      ;   5AutoTubeWpf.Views.BoolToProcessingButtonTextConverter  5�   "   5�      ��5�      �� 5�      $ IsProcessing�6"   $ 
LightGreen��66    5�      <	   6AutoTubeWpf.Views.BoolToProcessingButtonColorConverter	  5�   "   5�      5�      5�      5�      5�      