foreach ($job in $jobs) {
	$running = 0
	$progress = 0
	$speed = 0
	$state = $job.GetLastState()
	if ($state -ne "Stopped" -and $state -ne "Idle") {
		$sess = $job.FindLastSession()
		$progress = $sess.BaseProgress 
		$speed =  $sess.Progress.AvgSpeed
		$running = 1;
	} 
	New-Object -TypeName psobject -Property @{jobname=$job.name;speed=$speed;progress=$progress;state=$state;running=$running} 
}